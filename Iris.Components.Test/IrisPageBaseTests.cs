using System.Text.Json;
using FluentAssertions;
using Iris.Components.Shared.Snackbar;
using Iris.Contracts.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using NSubstitute;

namespace Iris.Components.Test;

public class IrisPageBaseTests : IrisTestContext
{
    private readonly ISnackbar _snackbar;
    
    public IrisPageBaseTests()
    {
        _snackbar = Substitute.For<ISnackbar>();
        
        Services.RemoveAll(typeof(ISnackbar));
        
        Services.AddSingleton(_snackbar);
    }

    [Fact(DisplayName = "Can construct base class")]
    public void Can_Construct_BaseClass()
    {
        // Arrange & Act
        var page = new IrisPageBase();

        // Assert
        page.Should().NotBeNull();
    }

    [Fact(DisplayName = "Can handle api error responses")]
    public void Can_Handle_ApiErrorResponses()
    {
        // Arrange
        var page = RenderComponent<IrisPageBase>();
        var error = new ApiErrorResponse()
        {
            StatusCode = 400,
            Errors = new Dictionary<string, List<string>>()
            {
                { "error", new List<string> { "error" } },
                { "data", new List<string> { "data" } },
            },
            Message = "error"
        };
        
        var msg = JsonSerializer.Serialize(error);
        var result = new Failure<bool>(msg);
        
        // Act
        page.Instance.HandleResult(result, "success", result.Message);
        
        // Assert
        _snackbar.Received()
            .Add<IrisSnackbar>(Arg.Is<Dictionary<string, object>>(d => d.Values.Any(s => s is string &&  s.ToString().Contains("error") && s.ToString().Contains("data") && !s.ToString().Contains('{'))),
                Severity.Error); 
    }
    
    [Fact(DisplayName = "Can handle api success responses")]
    public void Can_Handle_ApiSuccessResponses()
    {
        // Arrange
        var page = RenderComponent<IrisPageBase>();
        
        var result = new Success<bool>(true, "test");
        
        // Act
        page.Instance.HandleResult(result, result.Message, result.Message);
        
        // Assert
        _snackbar.Received()
            .Add<IrisSnackbar>(Arg.Is<Dictionary<string, object>>(d => d.Values.Any(s => s is string &&  s.ToString().Contains("test"))),
                Severity.Success); 
    }
    
    [Fact(DisplayName = "Can handle nonsensical format error responses")]
    public void Can_Handle_BadErrorResponses()
    {
        // Arrange
        var page = RenderComponent<IrisPageBase>();
        var error = new
        {
            testStatusCode = 400,
            bigErrors = new Dictionary<string, List<string>>()
            {
                { "error", new List<string> { "error" } },
                { "data", new List<string> { "data" } },
            },
            testMessage = "error"
        };
        
        var result = new Failure<bool>("Error");
        
        // Act
        page.Instance.HandleResult(result, "success", result.Message);
        
        // Assert
        _snackbar.Received()
            .Add<IrisSnackbar>(Arg.Is<Dictionary<string, object>>(d => d.Values.Any(s => s is string &&  s.ToString().Equals("Error"))),
                Severity.Error); 
    }
}