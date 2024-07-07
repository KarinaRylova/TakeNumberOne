using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using Telegram.Bot.Types;


public class JiraClient
{
    private HttpClient _httpClient;
    private string _jiraUrl;

    public void SetUrl(string jiraUrl)
    {
        _httpClient = new HttpClient();
        _jiraUrl = jiraUrl;
    }

    public void SetAuthentication(string username, string apiToken)
    {
        try
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiToken))
            {
                throw new ArgumentException("Username and API token must not be null or empty.");
            }

            if (_httpClient == null)
            {
                throw new InvalidOperationException("HttpClient is not initialized. Please set the Jira URL first using SetUrl method.");
            }

            var authToken = Encoding.ASCII.GetBytes($"{username}:{apiToken}");
            var authHeaderValue = Convert.ToBase64String(authToken);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);
            Console.WriteLine("Authentication set successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting authentication: {ex.Message}");
        }
    }

    public async Task<List<Project>> GetProjectsAsync()
    {
        try
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("HttpClient is not initialized. Please set the Jira URL first using SetUrl method.");
            }

            var url = $"{_jiraUrl}/rest/api/2/project";
            Console.WriteLine($"Requesting URL: {url}");
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {response.StatusCode} - {response.ReasonPhrase}");
            Console.WriteLine($"Content: {content}");
            if (!response.IsSuccessStatusCode)
            {
                return new List<Project>(); // Возвращаем пустой список при ошибке
            }
            return JsonConvert.DeserializeObject<List<Project>>(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred: {ex.Message}");
            return new List<Project>(); // Возвращаем пустой список при исключении
        }
    }

    public async Task<List<User>> GetProjectMembersAsync(string projectKey)
    {
        try
        {
            var url = $"{_jiraUrl}/rest/api/2/user/assignable/search?project={projectKey}";
            Console.WriteLine($"Requesting URL: {url}");
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {response.StatusCode} - {response.ReasonPhrase}");
            Console.WriteLine($"Content: {content}");
            if (!response.IsSuccessStatusCode)
            {
                return new List<User>(); // Возвращаем пустой список при ошибке
            }
            return JsonConvert.DeserializeObject<List<User>>(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred: {ex.Message}");
            return new List<User>(); // Возвращаем пустой список при исключении
        }
    }


    public async Task<List<Issue>> GetAllIssuesAsync()
    {
        try
        {
            var url = $"{_jiraUrl}/rest/api/2/search?jql=assignee is not EMPTY";
            Console.WriteLine($"Requesting URL: {url}");
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {response.StatusCode} - {response.ReasonPhrase}");
            Console.WriteLine($"Content: {content}");
            if (!response.IsSuccessStatusCode)
            {
                return new List<Issue>(); // Возвращаем пустой список при ошибке
            }
            var result = JsonConvert.DeserializeObject<SearchResult>(content);
            return result.Issues;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred: {ex.Message}");
            return new List<Issue>(); // Возвращаем пустой список при исключении
        }
    }

    public async Task<List<Issue>> GetProjectIssuesAsync(string projectKey)
    {
        try
        {
            var url = $"{_jiraUrl}/rest/api/2/search?jql=project=\"{projectKey}\"";
            Console.WriteLine($"Requesting URL: {url}");
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {response.StatusCode} - {response.ReasonPhrase}");
            Console.WriteLine($"Content: {content}");
            if (!response.IsSuccessStatusCode)
            {
                return new List<Issue>(); // Возвращаем пустой список при ошибке
            }
            var result = JsonConvert.DeserializeObject<SearchResult>(content);
            return result.Issues;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred: {ex.Message}");
            return new List<Issue>(); // Возвращаем пустой список при исключении
        }
    }

    public async Task<string> GetUserIssuesByDisplayNameAndProjectKeyAsync(string displayName, string projectKey)
    {
        try
        {
            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(projectKey))
            {
                return string.Empty;
            }

            var url = $"{_jiraUrl}/rest/api/2/search?jql=assignee=\"{displayName}\" AND project=\"{projectKey}\"";
            Console.WriteLine($"Requesting URL: {url}");
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {response.StatusCode} - {response.ReasonPhrase}");
            Console.WriteLine($"Content: {content}");
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }
            var result = JsonConvert.DeserializeObject<SearchResult>(content);

            // Группируем задачи по статусу
            var issuesGroupedByStatus = result.Issues.GroupBy(i => i.Fields.Status.Name).ToList();

            // Форматируем вывод
            var formattedOutput = new StringBuilder();
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

            return formattedOutput.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred: {ex.Message}");
            return string.Empty;
        }
    }


    private string GetStatusIcon(string status)
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
            Console.WriteLine($"Unknown status: {status}"); // For debugging, you can remove this later
            return "❓";
        }
    }

    public async Task<Issue> GetIssueAsync(string issueKey)
    {
        try
        {
            var url = $"{_jiraUrl}/rest/api/2/issue/{issueKey}";
            Console.WriteLine($"Requesting URL: {url}");
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {response.StatusCode} - {response.ReasonPhrase}");
            Console.WriteLine($"Content: {content}");
            if (!response.IsSuccessStatusCode)
            {
                return null; // Возвращаем null при ошибке
            }
            return JsonConvert.DeserializeObject<Issue>(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred: {ex.Message}");
            return null; // Возвращаем null при исключении
        }
    }
}

public class Project
{
    public string Key { get; set; }
    public string Name { get; set; }
}

public class User
{
    public string DisplayName { get; set; }
    public string EmailAddress { get; set; }
}

public class Issue
{
    public string Key { get; set; }
    public Fields Fields { get; set; }
}

public class Fields
{
    public string Summary { get; set; }
    public User Assignee { get; set; }
    public Status Status { get; set; }
}

public class Status
{
    public string Name { get; set; }
}

public class SearchResult
{
    public List<Issue> Issues { get; set; }
}
public class JiraWebhookEvent
{
    public string WebhookEvent { get; set; }
    public Issue Issue { get; set; }
    // Дополнительные поля в зависимости от типа webhook
}
