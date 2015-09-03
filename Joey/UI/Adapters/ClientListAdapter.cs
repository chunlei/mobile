﻿using System;
using System.Collections.Specialized;
using Android.Content;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;
using PopupArgs = Android.Widget.PopupMenu.MenuItemClickEventArgs;

namespace Toggl.Joey.UI.Adapters
{
    public class ClientListAdapter : RecycledDataViewAdapter<object>
    {
        protected static readonly int ViewTypeContent = 1;
        protected static readonly int ViewTypeNewClient = ViewTypeContent;
        protected static readonly int ViewTypeClient = ViewTypeContent + 1;
        protected static readonly int ViewTypeLoaderPlaceholder = 0;
        public const long CreateClientId = -1;


        public Action<object> HandleClientSelection { get; set; }

        public ClientListAdapter (RecyclerView owner, WorkspaceClientsView collectionView) : base (owner, collectionView)
        {
        }

        protected override void CollectionChanged (NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) {
                NotifyDataSetChanged();
            }
        }

        protected override RecyclerView.ViewHolder GetViewHolder (ViewGroup parent, int viewType)
        {
            View view;
            RecyclerView.ViewHolder holder;
            if (viewType == ViewTypeNewClient) {
                view = LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ()).Inflate (Resource.Layout.ClientListClientItem, parent, false);
                holder = new NewClientListItemHolder (this, view);
            } else {
                view = LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ()).Inflate (Resource.Layout.ClientListClientItem, parent, false);
                holder = new ClientListItemHolder (this, view);
            }
            return holder;
        }

        private void HandleClientItemClick (int position)
        {
            var proj = (WorkspaceClientsView.Client)GetEntry (position);
            var handler = HandleClientSelection;
            if (handler != null) {
                handler (proj);
            }
            return;
        }

        protected override void BindHolder (RecyclerView.ViewHolder holder, int position)
        {
            var viewType = GetItemViewType (position);

            var data = (WorkspaceClientsView.Client) GetEntry (position);
            if (viewType == ViewTypeClient) {
                var clientHolder = (ClientListItemHolder)holder;
                clientHolder.Bind (data);
            } else if ( viewType == ViewTypeNewClient) {
                var clientHolder = (NewClientListItemHolder)holder;
                clientHolder.Bind (data);
            }
        }

        public override int GetItemViewType (int position)
        {
            if (position == DataView.Count) {
                return ViewTypeLoaderPlaceholder;
            }
            var obj = GetEntry (position);
            var p = (WorkspaceClientsView.Client)obj;
            return p.IsNewClient ? ViewTypeNewClient : ViewTypeClient;
        }

        #region View holders

        public class ClientListItemHolder : RecycledBindableViewHolder<WorkspaceClientsView.Client>, View.IOnClickListener
        {
            private readonly ClientListAdapter adapter;

            private ClientModel model;

            public TextView ClientTextView { get; private set; }

            public ClientListItemHolder (ClientListAdapter adapter, View root) : base (root)
            {
                this.adapter = adapter;
                ClientTextView = root.FindViewById<TextView> (Resource.Id.ClientTextView).SetFont (Font.RobotoLight);
                root.SetOnClickListener (this);
            }

            public void OnClick (View v)
            {
                adapter.HandleClientSelection (DataSource);
            }

            protected async override void Rebind ()
            {
                // Protect against Java side being GCed
                if (Handle == IntPtr.Zero) {
                    return;
                }

                model = null;
                if (DataSource != null) {
                    model = (ClientModel)DataSource.Data;
                }

                await model.LoadAsync ();

                ClientTextView.Text = model.Name;
            }
        }

        public class NewClientListItemHolder : RecycledBindableViewHolder<WorkspaceClientsView.Client>, View.IOnClickListener
        {
            private readonly ClientListAdapter adapter;

            public TextView ClientTextView { get; private set; }

            public NewClientListItemHolder (IntPtr a, Android.Runtime.JniHandleOwnership b) : base (a, b)
            {
            }

            public NewClientListItemHolder (ClientListAdapter adapter, View root) : base (root)
            {
                this.adapter = adapter;
                ClientTextView = root.FindViewById<TextView> (Resource.Id.ClientTextView).SetFont (Font.Roboto);
                root.SetOnClickListener (this);
            }

            protected override void Rebind ()
            {
                ClientTextView.SetText (Resource.String.ClientsNewClient);
            }

            public void OnClick (View v)
            {
                adapter.HandleClientSelection (DataSource);
            }
        }
        #endregion
    }
}