using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text;

class Program
{
    private static readonly TelegramBotClient Bot = new TelegramBotClient("7285961122:AAGeSwhQ64_sd8HqWcgHaX4_VN1702vHC4g");
    private static readonly JiraClient Jira = new JiraClient();
    private static string _username;
    private static string _apiToken;
    private static string _jiraUrl;

    static async Task Main()
    {
        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // получать все типы обновлений
        };

        Bot.StartReceiving(
            new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync),
            receiverOptions,
            cancellationToken: cts.Token
        );

        Console.WriteLine("Bot started...");
        Console.ReadLine();

        cts.Cancel();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {

        if (update.Type == UpdateType.Message && update.Message.Text != null)
        {
            var message = update.Message;

            if (message.Text.StartsWith("/seturl"))
            {
                var parts = message.Text.Split(' ');
                if (parts.Length == 2)
                {
                    _jiraUrl = parts[1];
                    Jira.SetUrl(_jiraUrl);
                    await Bot.SendTextMessageAsync(message.Chat.Id, "Jira URL set successfully!", cancellationToken: cancellationToken);
                }
                else
                {
                    await Bot.SendTextMessageAsync(message.Chat.Id, "Usage: /seturl <jiraUrl>", cancellationToken: cancellationToken);
                }
            }
            else if (message.Text.StartsWith("/login"))
            {
                var parts = message.Text.Split(' ');
                if (parts.Length == 3)
                {
                    _username = parts[1];
                    _apiToken = parts[2];
                    Jira.SetAuthentication(_username, _apiToken);
                    await Bot.SendTextMessageAsync(message.Chat.Id, "Logged in successfully!", cancellationToken: cancellationToken);
                }
                else
                {
                    await Bot.SendTextMessageAsync(message.Chat.Id, "Usage: /login <username> <apiToken>", cancellationToken: cancellationToken);
                }
            }
            else if (message.Text.StartsWith("/projects"))
            {
                if (_username == null || _apiToken == null)
                {
                    await Bot.SendTextMessageAsync(message.Chat.Id, "Please login first using /login <username> <apiToken>", cancellationToken: cancellationToken);
                    return;
                }

                try
                {
                    var projects = await Jira.GetProjectsAsync();
                    if (projects.Any())
                    {
                        var buttons = projects.Select(p => InlineKeyboardButton.WithCallbackData(p.Name, "project_" + p.Key)).ToArray();
                        var keyboard = new InlineKeyboardMarkup(buttons);


                        await Bot.SendTextMessageAsync(message.Chat.Id, "Projects:", replyMarkup: keyboard, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await Bot.SendTextMessageAsync(message.Chat.Id, "No projects found or an error occurred.", cancellationToken: cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    await Bot.SendTextMessageAsync(message.Chat.Id, $"An error occurred while fetching projects: {ex.Message}", cancellationToken: cancellationToken);
                }
            }
            else if (message.Text.StartsWith("/allissues"))
            {
                var parts = message.Text.Split(' ');
                if (parts.Length == 2)
                {
                    var projectKey = parts[1];
                    try
                    {
                        var issues = await Jira.GetProjectIssuesAsync(projectKey);
                        if (issues.Any())
                        {
                            var formattedOutput = new StringBuilder();

                            // Группируем задачи по статусу
                            var issuesGroupedByStatus = issues.GroupBy(i => i.Fields.Status.Name).ToList();
                            foreach (var group in issuesGroupedByStatus)
                            {
                                var statusIcon = GetStatusIcon(group.Key);
                                formattedOutput.AppendLine($"{statusIcon} {group.Key.ToUpperInvariant()}:");
                                foreach (var issue in group)
                                {
                                    formattedOutput.AppendLine($"- {issue.Key}: {issue.Fields.Summary}");
                                }
                                formattedOutput.AppendLine(); // Добавляем пустую строку между группами
                            }

                            await Bot.SendTextMessageAsync(message.Chat.Id, $"Issues in project {projectKey}:\n{formattedOutput}", cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await Bot.SendTextMessageAsync(message.Chat.Id, $"No issues found for project {projectKey} or an error occurred.", cancellationToken: cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        await Bot.SendTextMessageAsync(message.Chat.Id, $"An error occurred while fetching project issues: {ex.Message}", cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    await Bot.SendTextMessageAsync(message.Chat.Id, "Usage: /allissues <projectKey>", cancellationToken: cancellationToken);
                }
            }
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            var callbackQuery = update.CallbackQuery;
            if (callbackQuery != null)
            {
                var data = callbackQuery.Data;
                if (data.StartsWith("project_"))
                {
                    var projectKey = data.AsSpan("project_".Length).ToString();
                    try
                    {
                        var members = await Jira.GetProjectMembersAsync(projectKey);
                        if (members.Any())
                        {
                            var buttons = members.Select(m => InlineKeyboardButton.WithCallbackData(m.DisplayName, $"member_{m.DisplayName}_project_{projectKey}")).ToArray();
                            var keyboard = new InlineKeyboardMarkup(buttons);
                            await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Members of project {projectKey}:", replyMarkup: keyboard, cancellationToken: cancellationToken);
                        }
                        else
                        {

                            await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"No members found for project {projectKey} or an error occurred.", cancellationToken: cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"An error occurred while fetching project members: {ex.Message}", cancellationToken: cancellationToken);
                    }
                }
                else if (data.StartsWith("member_"))
                {
                    var parts = data.Split("_project_");
                    var displayName = parts[0].Substring("member_".Length);
                    var projectKey = parts[1];
                    try
                    {
                        var issues = await Jira.GetUserIssuesByDisplayNameAndProjectKeyAsync(displayName, projectKey);
                        if (!string.IsNullOrEmpty(issues))
                        {
                            await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Issues assigned to {displayName} in project {projectKey}:\n{issues}", cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"No issues found for user {displayName} in project {projectKey} or an error occurred.", cancellationToken: cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"An error occurred while fetching user issues: {ex.Message}", cancellationToken: cancellationToken);
                    }
                }
            }
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine(exception.Message);
        return Task.CompletedTask;
    }

    private static string GetStatusIcon(string status)
    {
        var statusIconMap = new Dictionary<string, string>
    {
        { "To Do", "📝" },
        { "К выполнению", "📝" },
        { "In Progress", "🔄" },
        { "В работе", "🔄" },
        { "Done", "✅" },
        { "Готово", "✅" },
        { "Open", "🔓" },
        { "Открыто", "🔓" },
        { "Closed", "🔒" },
        { "Закрыто", "🔒" },
        { "In Review", "🔍" },
        { "Ревью", "🔍" },
        { "Resolved", "✔️" },
        { "Решено", "✔️" },
        { "Testing", "🧪" },
        { "Тестирование", "🧪" }
    };

        if (statusIconMap.TryGetValue(status, out var icon))
        {
            return icon;
        }
        else
        {
            Console.WriteLine($"Unknown status: {status}"); // Для отладки, вы можете убрать это позже
            return "❓";
        }
    }
}
