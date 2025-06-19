namespace Iris.Contracts.Templates.Endpoints
{
    public static class DeleteTemplate
    {
        public record DeleteTemplateRequest(Guid TemplateId);

        public record DeleteTemplateResponse(Results.Result<bool> Result);
    }
}

