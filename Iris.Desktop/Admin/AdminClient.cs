using System.Net.Http.Json;
using Iris.Components.Admin;
using Iris.Contracts.Admin;
using static Iris.Contracts.Admin.SubmitBugReport;

namespace Iris.Desktop.Admin;

public class AdminClient : IAdminService
{
    private readonly HttpClient _client;
    
    public AdminClient(IHttpClientFactory factory)
    {
        _client = factory.CreateClient("Iris");
    }

    public async Task<bool> SubmitBugReportAsync(BugType bugType, string message)
    {
        var req = new SubmitBugReportRequest(new BugReport(bugType, message));

        var response = await _client.PostAsJsonAsync("/api/admin/bugreport", req);

        return response.IsSuccessStatusCode;
    }
}