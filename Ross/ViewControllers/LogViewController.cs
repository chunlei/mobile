using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Timers;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using GalaSoft.MvvmLight.Helpers;
using Toggl.Phoebe;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Reactive;
using Toggl.Phoebe.ViewModels;
using Toggl.Phoebe.ViewModels.Timer;
using Toggl.Ross.DataSources;
using Toggl.Ross.Theme;
using Toggl.Ross.Views;
using UIKit;

namespace Toggl.Ross.ViewControllers
{
    public class LogViewController : UITableViewController
    {
        private const string DefaultDurationText = " 00:00:00 ";
        readonly static NSString EntryCellId = new NSString ("EntryCellId");
        readonly static NSString SectionHeaderId = new NSString ("SectionHeaderId");

        private SimpleEmptyView defaultEmptyView;
        private UIView obmEmptyView;
        private UIView reloadView;
        private UIButton durationButton;
        private UIButton actionButton;
        private UIBarButtonItem navigationButton;
        private UIActivityIndicatorView defaultFooterView;

        private Binding<LogTimeEntriesVM.LoadInfoType, LogTimeEntriesVM.LoadInfoType> loadInfoBinding;
        private Binding<int, int> hasItemsBinding, loadMoreBinding;
        private Binding<string, string> durationBinding;
        private Binding<bool, bool> syncBinding, hasErrorBinding, isRunningBinding;
        private Binding<ObservableCollection<IHolder>, ObservableCollection<IHolder>> collectionBinding;

        protected LogTimeEntriesVM ViewModel { get; set;}

        public LogViewController () : base (UITableViewStyle.Plain)
        {
        }

        public override void LoadView()
        {
            base.LoadView();

            NavigationItem.LeftBarButtonItem = new UIBarButtonItem (
                Image.IconNav.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal),
                UIBarButtonItemStyle.Plain, OnNavigationButtonTouched);

            defaultEmptyView = new SimpleEmptyView {
                Title = "LogEmptyTitle".Tr (),
                Message = "LogEmptyMessage".Tr (),
            };

            obmEmptyView = new OBMEmptyView {
                Title = "LogOBMEmptyTitle".Tr (),
                Message = "LogOBMEmptyMessage".Tr (),
            };

            reloadView = new ReloadTableViewFooter () {
                SyncButtonPressedHandler = OnTryAgainBtnPressed
            };

            // Setup top toolbar
            if (durationButton == null) {
                durationButton = new UIButton ().Apply (Style.NavTimer.DurationButton);
                durationButton.SetTitle (DefaultDurationText, UIControlState.Normal); // Dummy content to use for sizing of the label
                durationButton.SizeToFit ();
                durationButton.TouchUpInside += OnDurationButtonTouchUpInside;
            }

            if (navigationButton == null) {
                actionButton = new UIButton ().Apply (Style.NavTimer.StartButton);
                actionButton.SizeToFit ();
                actionButton.TouchUpInside += OnActionButtonTouchUpInside;
                navigationButton = new UIBarButtonItem (actionButton);
            }

            // Attach views
            var navigationItem = NavigationItem;
            navigationItem.TitleView = durationButton;
            navigationItem.RightBarButtonItem = navigationButton;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            EdgesForExtendedLayout = UIRectEdge.None;
            TableView.RegisterClassForCellReuse (typeof (TimeEntryCell), EntryCellId);
            TableView.RegisterClassForHeaderFooterViewReuse (typeof (SectionHeaderView), SectionHeaderId);
            TableView.SetEditing (false, true);

            // Create view model
            ViewModel = new LogTimeEntriesVM (StoreManager.Singleton.AppState);
            ViewModel.PropertyChanged += (sender, e) => {
                //Console.WriteLine (e.PropertyName);
            };

            var headerView = new TableViewRefreshView ();
            RefreshControl = headerView;
            headerView.AdaptToTableView (TableView);
            headerView.ValueChanged += (sender, e) => ViewModel.TriggerFullSync ();

            // Bindings
            syncBinding = this.SetBinding (() => ViewModel.IsFullSyncing).WhenSourceChanges (() => {
                if (!ViewModel.IsFullSyncing) {
                    headerView.EndRefreshing ();
                }
            });

            loadInfoBinding = this.SetBinding (() => ViewModel.LoadInfo).WhenSourceChanges (SetFooterState);
            hasItemsBinding = this.SetBinding (() => ViewModel.Collection.Count).WhenSourceChanges (SetCollectionState);
            //loadMoreBinding = this.SetBinding (() => ViewModel.Collection.Count).WhenSourceChanges (LoadMoreIfNeeded);
            collectionBinding = this.SetBinding (() => ViewModel.Collection).WhenSourceChanges (() => {
                TableView.Source = new TimeEntriesSource (this, ViewModel);
            });
            isRunningBinding = this.SetBinding (() => ViewModel.IsEntryRunning).WhenSourceChanges (SetStartStopButtonState);
            durationBinding = this.SetBinding (() => ViewModel.Duration).WhenSourceChanges (() => durationButton.SetTitle (ViewModel.Duration, UIControlState.Normal));
        }

        public override void ViewWillDisappear (bool animated)
        {
            if (IsMovingFromParentViewController) {
                ViewModel.Dispose ();
            }
            base.ViewWillDisappear (animated);
        }

        public override void ViewDidLayoutSubviews ()
        {
            base.ViewDidLayoutSubviews ();
            defaultEmptyView.Frame = new CGRect (25f, (View.Frame.Size.Height - 200f) / 2, View.Frame.Size.Width - 50f, 200f);
            obmEmptyView.Frame = new CGRect (25f, 15f, View.Frame.Size.Width - 50f, 200f);
            reloadView.Bounds = new CGRect (0f, 0f, View.Frame.Size.Width, 70f);
            reloadView.Center = new CGPoint (View.Center.X, reloadView.Center.Y);
        }

        private void OnDurationButtonTouchUpInside (object sender, EventArgs e)
        {

        }

        private void OnActionButtonTouchUpInside (object sender, EventArgs e)
        {
            // Send experiment data.
            ViewModel.ReportExperiment (OBMExperimentManager.StartButtonActionKey,
                                        OBMExperimentManager.ClickActionValue);
            ViewModel.StartStopTimeEntry ();
            /*
            if (entry.State == TimeEntryState.Running) {
                // Show next viewController.
                var controllers = new List<UIViewController> (NavigationController.ViewControllers);
                var editController = new EditTimeEntryViewController (entry, tagList);
                controllers.Add (editController);
                if (ServiceContainer.Resolve<SettingsStore> ().ChooseProjectForNew) {
                    controllers.Add (new ProjectSelectionViewController (entry.WorkspaceId, editController));
                }
                NavigationController.SetViewControllers (controllers.ToArray (), true);
            }
            */
        }

        private void SetStartStopButtonState ()
        {
            if (ViewModel.IsEntryRunning) {
                actionButton.Apply (Style.NavTimer.StopButton);
            } else {
                actionButton.Apply (Style.NavTimer.StartButton);
            }
        }

        private void LoadMoreIfNeeded ()
        {
            // TODO: Small hack due to the scroll needs more than the
            // 10 items to work correctly and load more itens.
            if (ViewModel.Collection.Count > 0 && ViewModel.Collection.Count < 10) {
                Console.WriteLine (" cuandooooooo!!");
                //ViewModel.LoadMore ();
            }
        }

        private void SetFooterState ()
        {
            if (ViewModel.LoadInfo.HasMore && !ViewModel.LoadInfo.HadErrors) {
                if (defaultFooterView == null) {
                    defaultFooterView = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
                    defaultFooterView.Frame = new CGRect (0, 0, 50, 50);
                    defaultFooterView.StartAnimating ();
                }
                TableView.TableFooterView = defaultFooterView;
            } else if (ViewModel.LoadInfo.HasMore && ViewModel.LoadInfo.HadErrors) {
                TableView.TableFooterView = reloadView;
            } else if (!ViewModel.LoadInfo.HasMore && !ViewModel.LoadInfo.HadErrors) {
                SetCollectionState ();
            }
        }

        private void SetCollectionState ()
        {
            if (ViewModel.LoadInfo.IsSyncing && ViewModel.Collection.Count == 0) {
                return;
            }

            UIView emptyView = defaultEmptyView; // Default empty view.
            var isWelcome = ViewModel.IsWelcomeMessageShown ();
            var hasItems = ViewModel.Collection.Count > 0;
            var isInExperiment = ViewModel.IsInExperiment ();

            // According to settings, show welcome message or no.
            ((SimpleEmptyView)emptyView).Title = isWelcome ? "LogWelcomeTitle".Tr () : "LogEmptyTitle".Tr ();

            if (isWelcome && isInExperiment) {
                emptyView = obmEmptyView;
            }

            TableView.TableFooterView = hasItems ? new UIView () : emptyView;
        }

        private void OnCountinueTimeEntry (int index)
        {
            ViewModel.ContinueTimeEntry (index);
        }

        private void OnTryAgainBtnPressed ()
        {
            ViewModel.LoadMore ();
        }

        private void OnNavigationButtonTouched (object sender, EventArgs e)
        {
            var main = AppDelegate.TogglWindow.RootViewController as MainViewController;
            main.ToggleMenu ();
        }

        #region TableViewSource
        class TimeEntriesSource : ObservableCollectionViewSource<IHolder, DateHolder, ITimeEntryHolder>
        {
            private bool isLoading;
            private readonly LogTimeEntriesVM VM;
            private LogViewController owner;

            public TimeEntriesSource (LogViewController owner, LogTimeEntriesVM viewModel) : base (owner.TableView, viewModel.Collection)
            {
                this.owner = owner;
                VM = viewModel;
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                var cell = (TimeEntryCell)tableView.DequeueReusableCell (EntryCellId, indexPath);
                var holder = (ITimeEntryHolder)collection.ElementAt (GetPlainIndexFromRow (collection, indexPath));
                cell.Bind (holder, OnContinueTimeEntry);
                return cell;
            }

            public override UIView GetViewForHeader (UITableView tableView, nint section)
            {
                var view = (SectionHeaderView)tableView.DequeueReusableHeaderFooterView (SectionHeaderId);
                view.Bind (collection.OfType<DateHolder> ().ElementAt ((int)section));
                return view;
            }

            public override nfloat GetHeightForHeader (UITableView tableView, nint section)
            {
                return EstimatedHeightForHeader (tableView, section);
            }

            public override nfloat EstimatedHeightForHeader (UITableView tableView, nint section)
            {
                return 42f;
            }

            public override nfloat EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
            {
                return 60f;
            }

            public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                return EstimatedHeight (tableView, indexPath);
            }

            public override bool CanEditRow (UITableView tableView, NSIndexPath indexPath)
            {
                return true;
            }

            public override void CommitEditingStyle (UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
            {
                if (editingStyle == UITableViewCellEditingStyle.Delete) {
                    var rowIndex = GetPlainIndexFromRow (collection, indexPath);
                    VM.RemoveTimeEntry (rowIndex);
                }
            }

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                var rowIndex = GetPlainIndexFromRow (collection, indexPath);
                var holder = collection.ElementAt (rowIndex) as ITimeEntryHolder;
                owner.NavigationController.PushViewController (new EditTimeEntryViewController (holder.Entry.Data.Id), true);
            }

            public override void Scrolled (UIScrollView scrollView)
            {
                var currentOffset = scrollView.ContentOffset.Y;
                var maximumOffset = scrollView.ContentSize.Height - scrollView.Frame.Height;

                if (isLoading) {
                    isLoading &= maximumOffset - currentOffset <= 200.0;
                }

                if (!isLoading && maximumOffset - currentOffset <= 200.0) {
                    isLoading = true;
                    VM.LoadMore ();
                }
            }

            private void OnContinueTimeEntry (TimeEntryCell cell)
            {
                var indexPath = TableView.IndexPathForCell (cell);
                var rowIndex = GetPlainIndexFromRow (collection, indexPath);
                VM.ContinueTimeEntry (rowIndex);
            }
        }
        #endregion

        #region Cells
        class TimeEntryCell : SwipableTimeEntryTableViewCell
        {
            private const float HorizPadding = 15f;
            private readonly UIView textContentView;
            private readonly UILabel projectLabel;
            private readonly UILabel clientLabel;
            private readonly UILabel taskLabel;
            private readonly UILabel descriptionLabel;
            private readonly UIImageView taskSeparatorImageView;
            private readonly UIImageView billableTagsImageView;
            private readonly UILabel durationLabel;
            private readonly UIImageView runningImageView;
            private bool isRunning;
            private TimeSpan duration;
            private Action<TimeEntryCell> OnContinueAction;
            private IDisposable timerSuscriber;

            public TimeEntryCell (IntPtr ptr) : base (ptr)
            {
                textContentView = new UIView ();
                projectLabel = new UILabel ().Apply (Style.Log.CellProjectLabel);
                clientLabel = new UILabel ().Apply (Style.Log.CellClientLabel);
                taskLabel = new UILabel ().Apply (Style.Log.CellTaskLabel);
                descriptionLabel = new UILabel ().Apply (Style.Log.CellDescriptionLabel);
                taskSeparatorImageView = new UIImageView ().Apply (Style.Log.CellTaskDescriptionSeparator);
                billableTagsImageView = new UIImageView ();
                durationLabel = new UILabel ().Apply (Style.Log.CellDurationLabel);
                runningImageView = new UIImageView ().Apply (Style.Log.CellRunningIndicator);

                textContentView.AddSubviews (
                    projectLabel, clientLabel,
                    taskLabel, descriptionLabel,
                    taskSeparatorImageView
                );

                var maskLayer = new CAGradientLayer () {
                    AnchorPoint = CGPoint.Empty,
                    StartPoint = new CGPoint (0.0f, 0.0f),
                    EndPoint = new CGPoint (1.0f, 0.0f),
                    Colors = new [] {
                        UIColor.FromWhiteAlpha (1, 1).CGColor,
                        UIColor.FromWhiteAlpha (1, 1).CGColor,
                        UIColor.FromWhiteAlpha (1, 0).CGColor,
                    },
                    Locations = new [] {
                        NSNumber.FromFloat (0f),
                        NSNumber.FromFloat (0.9f),
                        NSNumber.FromFloat (1f),
                    },
                };
                textContentView.Layer.Mask = maskLayer;

                ActualContentView.AddSubviews (
                    textContentView,
                    billableTagsImageView,
                    durationLabel,
                    runningImageView
                );
            }

            public void Bind (ITimeEntryHolder dataSource, Action<TimeEntryCell> OnContinueAction)
            {
                this.OnContinueAction = OnContinueAction;

                var projectName = "LogCellNoProject".Tr ();
                var projectColor = Color.Gray;
                var clientName = string.Empty;
                var info = dataSource.Entry.Info;

                if (!string.IsNullOrWhiteSpace (info.ProjectData.Name)) {
                    projectName = info.ProjectData.Name;
                    projectColor = UIColor.Clear.FromHex (ProjectData.HexColors [info.ProjectData.Color % ProjectData.HexColors.Length]);

                    if (!string.IsNullOrWhiteSpace (info.ClientData.Name)) {
                        clientName = info.ClientData.Name;
                    }
                }

                projectLabel.TextColor = projectColor;
                if (projectLabel.Text != projectName) {
                    projectLabel.Text = projectName;
                    SetNeedsLayout ();
                }
                if (clientLabel.Text != clientName) {
                    clientLabel.Text = clientName;
                    SetNeedsLayout ();
                }

                var taskName = info.TaskData.Name;
                var taskHidden = string.IsNullOrWhiteSpace (taskName);
                var description = dataSource.Entry.Data.Description;
                var descHidden = string.IsNullOrWhiteSpace (description);

                if (taskHidden && descHidden) {
                    description = "LogCellNoDescription".Tr ();
                    descHidden = false;
                }
                var taskDeskSepHidden = taskHidden || descHidden;

                if (taskLabel.Hidden != taskHidden || taskLabel.Text != taskName) {
                    taskLabel.Hidden = taskHidden;
                    taskLabel.Text = taskName;
                    SetNeedsLayout ();
                }
                if (descriptionLabel.Hidden != descHidden || descriptionLabel.Text != description) {
                    descriptionLabel.Hidden = descHidden;
                    descriptionLabel.Text = description;
                    SetNeedsLayout ();
                }
                if (taskSeparatorImageView.Hidden != taskDeskSepHidden) {
                    taskSeparatorImageView.Hidden = taskDeskSepHidden;
                    SetNeedsLayout ();
                }

                // Set duration
                duration = dataSource.GetDuration ();
                isRunning = dataSource.Entry.Data.State == TimeEntryState.Running;

                if (isRunning && timerSuscriber == null) {
                    Console.WriteLine ("RebindDuration!! " + GetHashCode() + " " + dataSource.Entry.Data.Id);
                    timerSuscriber = Observable.Timer (TimeSpan.FromMilliseconds (1000 - duration.Milliseconds), TimeSpan.FromSeconds (1))
                                     .Subscribe (x => RebindDuration ());
                } else if (!isRunning && timerSuscriber != null) {
                    Console.WriteLine ("unsuscribe " + GetHashCode() + " " + dataSource.Entry.Data.Id);
                    timerSuscriber.Dispose ();
                    timerSuscriber = null;
                }

                RebindTags (dataSource);
                RebindDuration ();
                LayoutIfNeeded ();

            }

            // Rebind duration with the saved state "lastDataSource"
            // TODO: Try to find a stateless method.
            private void RebindDuration ()
            {
                runningImageView.Hidden = !isRunning;
                duration = duration.Add (TimeSpan.FromSeconds (1000));
                durationLabel.Text = string.Format ("{0:D2}:{1:mm}:{1:ss}", (int)duration.TotalHours, duration);
            }


            private void RebindTags (ITimeEntryHolder dataSource)
            {
                var hasTags = dataSource.Entry.Data.TagIds.Count > 0;
                var isBillable = dataSource.Entry.Data.IsBillable;

                if (hasTags && isBillable) {
                    billableTagsImageView.Apply (Style.Log.BillableAndTaggedEntry);
                } else if (hasTags) {
                    billableTagsImageView.Apply (Style.Log.TaggedEntry);
                } else if (isBillable) {
                    billableTagsImageView.Apply (Style.Log.BillableEntry);
                } else {
                    billableTagsImageView.Apply (Style.Log.PlainEntry);
                }
            }

            protected override void OnContinueGestureFinished ()
            {
                if (OnContinueAction != null) {
                    OnContinueAction.Invoke (this);
                }
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();

                var contentFrame = ContentView.Frame;

                const float durationLabelWidth = 80f;
                durationLabel.Frame = new CGRect (
                    x: contentFrame.Width - durationLabelWidth - HorizPadding,
                    y: 0,
                    width: durationLabelWidth,
                    height: contentFrame.Height
                );

                const float billableTagsHeight = 20f;
                const float billableTagsWidth = 20f;
                billableTagsImageView.Frame = new CGRect (
                    y: (contentFrame.Height - billableTagsHeight) / 2,
                    height: billableTagsHeight,
                    x: durationLabel.Frame.X - billableTagsWidth,
                    width: billableTagsWidth
                );

                var runningHeight = runningImageView.Image.Size.Height;
                var runningWidth = runningImageView.Image.Size.Width;
                runningImageView.Frame = new CGRect (
                    y: (contentFrame.Height - runningHeight) / 2,
                    height: runningHeight,
                    x: contentFrame.Width - (HorizPadding + runningWidth) / 2,
                    width: runningWidth
                );

                textContentView.Frame = new CGRect (
                    x: 0, y: 0,
                    width: billableTagsImageView.Frame.X - 2f,
                    height: contentFrame.Height
                );
                textContentView.Layer.Mask.Bounds = textContentView.Frame;

                var bounds = GetBoundingRect (projectLabel);
                projectLabel.Frame = new CGRect (
                    x: HorizPadding,
                    y: contentFrame.Height / 2 - bounds.Height,
                    width: bounds.Width,
                    height: bounds.Height
                );

                const float clientLeftMargin = 7.5f;
                bounds = GetBoundingRect (clientLabel);
                clientLabel.Frame = new CGRect (
                    x: projectLabel.Frame.X + projectLabel.Frame.Width + clientLeftMargin,
                    y: (float)Math.Floor (projectLabel.Frame.Y + projectLabel.Font.Ascender - clientLabel.Font.Ascender),
                    width: bounds.Width,
                    height: bounds.Height
                );

                const float secondLineTopMargin = 3f;
                nfloat offsetX = HorizPadding + 1f;
                if (!taskLabel.Hidden) {
                    bounds = GetBoundingRect (taskLabel);
                    taskLabel.Frame = new CGRect (
                        x: offsetX,
                        y: contentFrame.Height / 2 + secondLineTopMargin,
                        width: bounds.Width,
                        height: bounds.Height
                    );
                    offsetX += taskLabel.Frame.Width + 4f;

                    if (!taskSeparatorImageView.Hidden) {
                        const float separatorOffsetY = -2f;
                        var imageSize = taskSeparatorImageView.Image != null ? taskSeparatorImageView.Image.Size : CGSize.Empty;
                        taskSeparatorImageView.Frame = new CGRect (
                            x: offsetX,
                            y: taskLabel.Frame.Y + taskLabel.Font.Ascender - imageSize.Height + separatorOffsetY,
                            width: imageSize.Width,
                            height: imageSize.Height
                        );

                        offsetX += taskSeparatorImageView.Frame.Width + 4f;
                    }

                    if (!descriptionLabel.Hidden) {
                        bounds = GetBoundingRect (descriptionLabel);
                        descriptionLabel.Frame = new CGRect (
                            x: offsetX,
                            y: (float)Math.Floor (taskLabel.Frame.Y + taskLabel.Font.Ascender - descriptionLabel.Font.Ascender),
                            width: bounds.Width,
                            height: bounds.Height
                        );

                        offsetX += descriptionLabel.Frame.Width + 4f;
                    }
                } else if (!descriptionLabel.Hidden) {
                    bounds = GetBoundingRect (descriptionLabel);
                    descriptionLabel.Frame = new CGRect (
                        x: offsetX,
                        y: contentFrame.Height / 2 + secondLineTopMargin,
                        width: bounds.Width,
                        height: bounds.Height
                    );
                }
            }

            private static CGRect GetBoundingRect (UILabel view)
            {
                var attrs = new UIStringAttributes {
                    Font = view.Font,
                };
                var rect = ((NSString) (view.Text ?? string.Empty)).GetBoundingRect (
                               new CGSize (float.MaxValue, float.MaxValue),
                               NSStringDrawingOptions.UsesLineFragmentOrigin,
                               attrs, null);
                rect.Height = (float)Math.Ceiling (rect.Height);
                return rect;
            }
        }

        class SectionHeaderView : UITableViewHeaderFooterView
        {
            private const float HorizSpacing = 15f;
            private readonly UILabel dateLabel;
            private readonly UILabel totalDurationLabel;
            private Timer timer;
            private bool isRunning;
            private TimeSpan duration;
            private DateTime date;

            public SectionHeaderView (IntPtr ptr) : base (ptr)
            {
                dateLabel = new UILabel ().Apply (Style.Log.HeaderDateLabel);
                ContentView.AddSubview (dateLabel);

                totalDurationLabel = new UILabel ().Apply (Style.Log.HeaderDurationLabel);
                ContentView.AddSubview (totalDurationLabel);

                BackgroundView = new UIView ().Apply (Style.Log.HeaderBackgroundView);
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                var contentFrame = ContentView.Frame;

                dateLabel.Frame = new CGRect (
                    x: HorizSpacing,
                    y: 0,
                    width: (contentFrame.Width - 3 * HorizSpacing) / 2,
                    height: contentFrame.Height
                );

                totalDurationLabel.Frame = new CGRect (
                    x: (contentFrame.Width - 3 * HorizSpacing) / 2 + 2 * HorizSpacing,
                    y: 0,
                    width: (contentFrame.Width - 3 * HorizSpacing) / 2,
                    height: contentFrame.Height
                );
            }

            public void Bind (DateHolder data)
            {
                date = data.Date;
                duration = data.TotalDuration;
                isRunning = data.IsRunning;
                SetContentData ();
            }

            private void SetContentData ()
            {
                if (timer != null) {
                    timer.Stop ();
                    timer.Elapsed -= OnDurationElapsed;
                    timer = null;
                }

                if (isRunning) {
                    timer = new Timer (60000 - duration.Seconds * 1000 - duration.Milliseconds);
                    timer.Elapsed += OnDurationElapsed;
                    timer.Start ();
                }

                dateLabel.Text = date.ToLocalizedDateString ();
                totalDurationLabel.Text = FormatDuration (duration);
            }

            private void OnDurationElapsed (object sender, ElapsedEventArgs e)
            {
                // Update duration with new time.
                duration = duration.Add (TimeSpan.FromMilliseconds (timer.Interval));
                InvokeOnMainThread (() => SetContentData ());
            }

            private string FormatDuration (TimeSpan dr)
            {
                if (dr.TotalHours >= 1f) {
                    return string.Format (
                               "LogHeaderDurationHoursMinutes".Tr (),
                               (int)dr.TotalHours,
                               dr.Minutes
                           );
                }

                if (dr.Minutes > 0) {
                    return string.Format (
                               "LogHeaderDurationMinutes".Tr (),
                               dr.Minutes
                           );
                }

                return string.Empty;
            }
        }
        #endregion
    }
}
