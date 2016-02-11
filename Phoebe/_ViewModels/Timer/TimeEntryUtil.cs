﻿using System;
using System.Collections.Generic;
using System.Linq;
using Toggl.Phoebe._Data;
using Toggl.Phoebe._Data.Diff;
using Toggl.Phoebe._Data.Models;
using XPlatUtils;
using Toggl.Phoebe._Reactive;

namespace Toggl.Phoebe._ViewModels.Timer
{
    // Empty interface just to hide references to IDiffComparable
    public interface IHolder : IDiffComparable
    {
    }

    public interface ITimeEntryHolder : IHolder
    {
        ITimeEntryData Data { get; }
        IList<ITimeEntryData> DataCollection { get; }
        TimeEntryInfo Info { get; }
        IList<string> Guids { get; }

        TimeSpan GetDuration ();
        DateTime GetStartTime ();
    }

    public enum TimeEntryGroupMethod {
        Single,
        ByDateAndTask
    }

    public class TimeEntryGrouper
    {
        readonly TimeEntryGroupMethod Method;

        public TimeEntryGrouper (TimeEntryGroupMethod method)
        {
            Method = method;
        }

        public IEnumerable<ITimeEntryHolder> Group (IEnumerable<TimeEntryHolder> items)
        {
            return Method == TimeEntryGroupMethod.Single
                   ? items.Cast<ITimeEntryHolder> () : TimeEntryGroup.Group (items);
        }

        public IEnumerable<TimeEntryHolder> Ungroup (IEnumerable<ITimeEntryHolder> groups)
        {
            return Method == TimeEntryGroupMethod.Single
                   ? groups.Cast<TimeEntryHolder> () : TimeEntryGroup.Ungroup (groups.Cast<TimeEntryGroup> ());
        }
    }

    public static class TimeEntryUtil
    {
        public static TimeEntryData CreateTimeEntryDraft ()
        {
            Guid userId = Guid.Empty;
            Guid workspaceId = Guid.Empty;
            bool durationOnly = false;

            var authManager = ServiceContainer.Resolve<Toggl.Phoebe.Net.AuthManager> ();
            if (authManager.IsAuthenticated) {
                var user = authManager.User;
                userId = user.Id;
                workspaceId = user.DefaultWorkspaceId;
                durationOnly = user.TrackingMode == TrackingMode.Continue;
            }

            // Create new draft object
            var newData = new TimeEntryData {
                State = TimeEntryState.New,
                UserId = userId,
                WorkspaceId = workspaceId,
                DurationOnly = durationOnly,
            };

            return newData;
        }
    }
}
