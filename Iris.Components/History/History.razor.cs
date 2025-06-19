using System;
using Iris.Components.History;
using Iris.Contracts.Audit;
using Iris.Contracts.Audit.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Iris.Components.History
{
    public partial class History : IrisPageBase
    {
        [Inject]
        protected AuthenticationStateProvider Auth { get; set; } = default!;

        public override string Identifier { get; set; } = "History";

        private List<AuditRecord>? HistoryRecords { get; set; } = new();

        public bool Loading = false;

        private string GetActionDisplay(string action)
        {
            if (Actions.DisplayLookup.ContainsKey(action))
            {
                return Actions.DisplayLookup[action];
            }

            return action;
        }

        protected override async Task OnInitializedAsync()
        {
            if (HistoryRecords == null || HistoryRecords.Count < 1)
            {
                Loading = true;
                StateHasChanged();

                HistoryRecords = await HistoryState.GetUserHistoryAsync();
                Loading = false;
                StateHasChanged();
            }
        }
    }
}

