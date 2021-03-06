﻿using System;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using Battlehub.RTCommon;
using Battlehub.UIControls;
using Battlehub.RTSaveLoad2.Interface;
using System.Collections.Generic;
using Battlehub.Utils;

namespace Battlehub.RTEditor
{
    public class SelectionChangedArgs<T> : EventArgs
    {
        /// <summary>
        /// Unselected Items
        /// </summary>
        public T[] OldItems
        {
            get;
            private set;
        }

        /// <summary>
        /// Selected Items
        /// </summary>
        public T[] NewItems
        {
            get;
            private set;
        }

        /// <summary>
        /// First Unselected Item
        /// </summary>
        public T OldItem
        {
            get
            {
                if (OldItems == null)
                {
                    return default(T);
                }
                if (OldItems.Length == 0)
                {
                    return default(T);
                }
                return OldItems[0];
            }
        }

        /// <summary>
        /// First Selected Item
        /// </summary>
        public T NewItem
        {
            get
            {
                if (NewItems == null)
                {
                    return default(T);
                }
                if (NewItems.Length == 0)
                {
                    return default(T);
                }
                return NewItems[0];
            }
        }

        public bool IsUserAction
        {
            get;
            private set;
        }

        public SelectionChangedArgs(T[] oldItems, T[] newItems, bool isUserAction)
        {
            OldItems = oldItems;
            NewItems = newItems;
            IsUserAction = isUserAction;
        }

        public SelectionChangedArgs(T oldItem, T newItem, bool isUserAction)
        {
            OldItems = new[] { oldItem };
            NewItems = new[] { newItem };
            IsUserAction = isUserAction;
        }

        public SelectionChangedArgs(SelectionChangedArgs args, bool isUserAction)
        {
            if(args.OldItems != null)
            {
                OldItems = args.OldItems.OfType<T>().ToArray();
            }

            if(args.NewItems != null)
            {
                NewItems = args.NewItems.OfType<T>().ToArray();
            }
            IsUserAction = isUserAction;
        }
    }

    public class ProjectTreeEventArgs : EventArgs
    {
        public ProjectItem[] ProjectItems
        {
            get;
            private set;
        }

        public ProjectItem ProjectItem
        {
            get
            {
                if (ProjectItems == null || ProjectItems.Length == 0)
                {
                    return null;
                }
                return ProjectItems[0];
            }
        }

        public ProjectTreeEventArgs(ProjectItem[] projectItems)
        {
            ProjectItems = projectItems;
        }
    }

    public class ProjectTreeRenamedEventArgs : ProjectTreeEventArgs
    {
        public string[] OldNames
        {
            get;
            private set;
        }

        public string OldName
        {
            get
            {
                if (OldNames == null || OldNames.Length == 0)
                {
                    return null;
                }
                return OldNames[0];
            }
        }

        public ProjectTreeRenamedEventArgs(ProjectItem[] projectItems, string[] oldNames)
            : base(projectItems)
        {
            OldNames = oldNames;
        }
    }

    public class ProjectTreeView : RuntimeWindow
    {
        public event EventHandler<SelectionChangedArgs<ProjectItem>> SelectionChanged;
        public event EventHandler<ProjectTreeRenamedEventArgs> ItemRenamed;
        public event EventHandler<ProjectTreeEventArgs> ItemDeleted;
        //public event EventHandler<ItemDropArgs> Drop;

        private IProject m_project;

        [SerializeField]
        private GameObject TreeViewPrefab;
        [SerializeField]
        private Sprite FolderIcon;

        [SerializeField]
        private Sprite ExposedFolderIcon;
        private VirtualizingTreeView m_treeView;

        public KeyCode RemoveKey = KeyCode.Delete;
        [HideInInspector]
        public bool ShowRootFolder = true;

        [NonSerialized]
        private ProjectItem m_root;

        public ProjectItem[] SelectedFolders
        {
            get
            {
                return m_treeView.SelectedItemsCount > 0 ?
                    m_treeView.SelectedItems.OfType<ProjectItem>().ToArray() :
                    new ProjectItem[0];
            }
        }
      
        public ProjectItem SelectedFolder
        {
            get
            {
                return (ProjectItem)m_treeView.SelectedItem;
            }
            set
            {
                if(value == null)
                {
                    m_treeView.SelectedIndex = -1;
                }
                else
                {
                    ProjectItem folder = value;
                    string path = folder.ToString();
                    folder = m_root.Get(path);

                    if (folder != null)
                    {
                        if (folder.Parent == null)
                        {
                            Expand(folder);
                        }
                        else
                        {
                            Expand(folder.Parent);
                        }
                    }

                    if (m_treeView.IndexOf(folder) >= 0)
                    {
                        m_treeView.ScrollIntoView(folder);
                        m_treeView.SelectedItem = folder;
                    }
                }  
            }
        }

        private void Expand(ProjectItem item)
        {
            if (item == null)
            {
                return;
            }
            if (item.Parent != null && !m_treeView.IsExpanded(item.Parent))
            {
                Expand(item.Parent);
            }
            m_treeView.Expand(item);
        }


        private void Toggle(ProjectItem projectItem)
        {
            VirtualizingTreeViewItem treeViewItem = m_treeView.GetTreeViewItem(projectItem);
            if (treeViewItem == null)
            {
                Toggle(projectItem.Parent);
                treeViewItem = m_treeView.GetTreeViewItem(projectItem);
            }
            else
            {
                treeViewItem.IsExpanded = !treeViewItem.IsExpanded;
            }
        }

        protected override void AwakeOverride()
        {
            WindowType = RuntimeWindowType.ProjectTree;
            base.AwakeOverride();
            if(Editor == null)
            {
                Debug.LogError("Editor is null");
                return;
            }

            if(TreeViewPrefab == null)
            {
                Debug.LogError("TreeViewPrefab is null");
                return;
            }

            m_project = IOC.Resolve<IProject>();
           
            m_treeView = Instantiate(TreeViewPrefab).GetComponent<VirtualizingTreeView>();
            m_treeView.CanReorder = false;
            m_treeView.CanReparent = ShowRootFolder;
            m_treeView.CanUnselectAll = false;
            m_treeView.CanDrag = ShowRootFolder;
            m_treeView.CanRemove = false;

            m_treeView.transform.SetParent(transform, false);
            m_treeView.SelectionChanged += OnSelectionChanged;
            m_treeView.ItemDataBinding += OnItemDataBinding;
            m_treeView.ItemExpanding += OnItemExpanding;
            m_treeView.ItemsRemoving += OnItemsRemoving;
            m_treeView.ItemsRemoved += OnItemsRemoved;
            m_treeView.ItemBeginEdit += OnItemBeginEdit;
            m_treeView.ItemEndEdit += OnItemEndEdit;
            m_treeView.ItemBeginDrop += OnItemBeginDrop;
            m_treeView.ItemDrop += OnItemDrop;
            m_treeView.ItemDoubleClick += OnItemDoubleClick;
        }

        protected override void UpdateOverride()
        {
            base.UpdateOverride();
            if (Editor.ActiveWindow == this)
            {
                if (Editor.Input.GetKeyDown(RemoveKey))
                {
                    if (m_treeView.SelectedItem != null)
                    {
                        ProjectItem projectItem = (ProjectItem)m_treeView.SelectedItem;
                        if(projectItem.Parent == null)
                        {
                            PopupWindow.Show("Unable to Remove", "Unable to remove root folder", "OK");
                        }
                        else
                        {
                            PopupWindow.Show("Delete Selected Assets", "You cannot undo this action", "Delete", arg =>
                            {
                                m_treeView.RemoveSelectedItems();
                            },"Cancel");
                        }
                    }
                }
            }
        }

        protected override void OnDestroyOverride()
        {
            base.OnDestroyOverride();
            Unsubscribe();
        }
    
        private void Unsubscribe()
        {
            if(m_treeView != null)
            {
                m_treeView.SelectionChanged -= OnSelectionChanged;
                m_treeView.ItemDataBinding -= OnItemDataBinding;
                m_treeView.ItemExpanding -= OnItemExpanding;
                m_treeView.ItemsRemoving -= OnItemsRemoving;
                m_treeView.ItemsRemoved -= OnItemsRemoved;
                m_treeView.ItemBeginEdit -= OnItemBeginEdit;
                m_treeView.ItemEndEdit -= OnItemEndEdit;
                m_treeView.ItemBeginDrop -= OnItemBeginDrop;
                m_treeView.ItemDrop -= OnItemDrop;
                m_treeView.ItemDoubleClick -= OnItemDoubleClick;
            }
        }

        //private void OnMoveCompleted(Error error, ProjectItem[] projectItems, ProjectItem target)
        //{
        //    if (error.HasError)
        //    {
        //        PopupWindow.Show("Unable to move assets", error.ErrorText, "OK");
        //        return;
        //    }

        //    for (int i = 0; i < projectItems.Length; ++i)
        //    {
        //        ProjectItem projectItem = projectItems[i];
        //        if (!(projectItem is AssetItem))
        //        {
        //            m_treeView.ChangeParent(target, projectItem);
        //        }
        //        projectItem.AddChild(projectItem);
        //    }
        //}

        public void LoadProject(ProjectItem root)
        {
            if(root == null)
            {
                m_treeView.Items = null;
            }
            else
            {
                if (ShowRootFolder)
                {
                    m_treeView.Items = new[] { root };
                }
                else
                {
                    if (root.Children != null)
                    {
                        m_root.Children = root.Children.OrderBy(projectItem => projectItem.NameExt).ToList();
                        m_treeView.Items = m_root.Children.Where(projectItem => CanDisplayFolder(projectItem)).ToArray();
                    }
                }
            }
            
            m_root = root;
        }

        public void ChangeParent(ProjectItem[] projectItems)
        {
            for(int i = 0; i < projectItems.Length; ++i)
            {
                m_treeView.ChangeParent(projectItems[i].Parent, projectItems[i]);
            }
        }

        public void UpdateProjectItem(ProjectItem item)
        {
            VirtualizingItemContainer itemContainer = m_treeView.GetItemContainer(item);
            if (itemContainer != null)
            {
                m_treeView.DataBindItem(item, itemContainer);
            }
        }

        public void RemoveProjectItemsFromTree(ProjectItem[] projectItems)
        {
            for (int i = 0; i < projectItems.Length; ++i)
            {
                ProjectItem projectItem = projectItems[i];
                if (projectItem.IsFolder)
                {
                    bool isLastChild = projectItem.Parent == null || projectItem.Parent.Children.Where(p => p.IsFolder).Count() == 1;
                    m_treeView.RemoveChild(projectItem.Parent, projectItem, isLastChild);
                }
            }
        }

        private void OnItemDoubleClick(object sender, ItemArgs e)
        {
            ProjectItem projectItem = (ProjectItem)e.Items[0];
            Toggle(projectItem);
        }

        private bool CanDrop(ProjectItem dropFolder, object[] dragItems)
        {
            if(dropFolder == null)
            {
                return false;
            }

            if(dropFolder.Children == null)
            {
                return true;
            }


            ProjectItem[] dragProjectItems = dragItems.OfType<ProjectItem>().ToArray();
            if(dragProjectItems.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < dragProjectItems.Length; ++i)
            {
                ProjectItem dragItem = dragProjectItems[i];
                if(dropFolder.IsAncestorOf(dragItem))
                {
                    return false;
                }
                
                if(dropFolder.Children.Any(childItem => childItem.NameExt == dragItem.NameExt))
                {
                    return false;
                }
            }
            return true;
        }

      
        private void OnItemBeginDrop(object sender, ItemDropCancelArgs e)
        {
            if (!e.IsExternal)
            {
                ProjectItem dropFolder = (ProjectItem)e.DropTarget;
                e.Cancel = !CanDrop(dropFolder, e.DragItems.OfType<ProjectItem>().ToArray());
            }
        }

        private void OnItemDrop(object sender, ItemDropArgs e)
        {
            ProjectItem drop = (ProjectItem)e.DropTarget;
            if (e.Action == ItemDropAction.SetLastChild)
            {
                m_project.Move(e.DragItems.OfType<ProjectItem>().ToArray(), (ProjectItem)e.DropTarget);
            }
        }

        private void OnItemBeginEdit(object sender, VirtualizingTreeViewItemDataBindingArgs e)
        {
            ProjectItem item = e.Item as ProjectItem;
            if (item != null)
            {
                InputField inputField = e.EditorPresenter.GetComponentInChildren<InputField>(true);
                inputField.text = item.Name;
                inputField.ActivateInputField();
                inputField.Select();

                Image image = e.EditorPresenter.GetComponentInChildren<Image>(true);
                if(m_project.IsStatic(item))
                {
                    image.sprite = ExposedFolderIcon;
                }
                else
                {
                    image.sprite = FolderIcon;
                }
                image.sprite = FolderIcon;
                image.gameObject.SetActive(true);

                LayoutElement layout = inputField.GetComponent<LayoutElement>();

                Text text = e.ItemPresenter.GetComponentInChildren<Text>(true);
                text.text = item.Name;

                RectTransform rt = text.GetComponent<RectTransform>();
                layout.preferredWidth = rt.rect.width;
            }
        }

        private void OnItemEndEdit(object sender, VirtualizingTreeViewItemDataBindingArgs e)
        {
            InputField inputField = e.EditorPresenter.GetComponentInChildren<InputField>(true);
            Text text = e.ItemPresenter.GetComponentInChildren<Text>(true);

            ProjectItem projectItem = (ProjectItem)e.Item;
            string oldName = projectItem.Name;
            if (projectItem.Parent != null)
            {
                ProjectItem parentItem = projectItem.Parent;
                string newNameExt = inputField.text.Trim() + projectItem.Ext;
                if (!string.IsNullOrEmpty(inputField.text.Trim()) && ProjectItem.IsValidName(inputField.text.Trim()) && !parentItem.Children.Any(p => p.NameExt == newNameExt))
                {
                    projectItem.Name = inputField.text.Trim();
                }
            }

            if(projectItem.Name != oldName)
            {
                if (ItemRenamed != null)
                {
                    ItemRenamed(this, new ProjectTreeRenamedEventArgs(new[] { projectItem }, new[] { oldName }));
                }
            }
            
            text.text = projectItem.Name;

            //Following code is required to unfocus inputfield if focused and release InputManager
            if (EventSystem.current != null && !EventSystem.current.alreadySelecting)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void OnItemsRemoving(object sender, ItemsCancelArgs e)
        {
            if (e.Items == null)
            {
                return;
            }

            if (!Editor.ActiveWindow == this)
            {
                e.Items.Clear();
                return;
            }

            for (int i = e.Items.Count - 1; i >= 0; i--)
            {
                ProjectItem item = (ProjectItem)e.Items[i];
                if (m_project.IsStatic(item))
                {
                    e.Items.Remove(item);
                }
            }

            if (e.Items.Count == 0)
            {
                PopupWindow.Show("Can't remove folder", "Unable to remove folders exposed from editor", "OK");
            }
        }

        private void OnItemsRemoved(object sender, ItemsRemovedArgs e)
        {
            if(ItemDeleted != null)
            {
                ItemDeleted(this, new ProjectTreeEventArgs(e.Items.OfType<ProjectItem>().ToArray()));
            }
        }

        private void OnItemExpanding(object sender, VirtualizingItemExpandingArgs e)
        {
            ProjectItem item = e.Item as ProjectItem;
            if (item != null)
            {
                item.Children = item.Children
                    .OrderBy(projectItem => projectItem.NameExt).ToList();
                e.Children = item.Children
                    .Where(projectItem => CanDisplayFolder(projectItem))
                    .OrderBy(projectItem => projectItem.NameExt);
            }
        }

        private void OnItemDataBinding(object sender, VirtualizingTreeViewItemDataBindingArgs e)
        {
            ProjectItem item = e.Item as ProjectItem;
            if (item != null)
            {
                Text text = e.ItemPresenter.GetComponentInChildren<Text>(true);
                text.text = item.Name;

                Image image = e.ItemPresenter.GetComponentInChildren<Image>(true);
                if (m_project.IsStatic(item))
                {
                    image.sprite = ExposedFolderIcon;
                }
                else
                {
                    image.sprite = FolderIcon;
                }
                image.gameObject.SetActive(true);
                e.CanEdit = !m_project.IsStatic(item) && item.Parent != null;
                e.CanDrag = !m_project.IsStatic(item) && item.Parent != null;
                e.HasChildren = item.Children != null && item.Children.Count(projectItem => CanDisplayFolder(projectItem)) > 0;
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedArgs e)
        {
            if(SelectionChanged != null)
            {
                SelectionChanged(this, new SelectionChangedArgs<ProjectItem>(e, true));
            }
        }


        private bool CanDisplayFolder(ProjectItem projectItem)
        {
            return projectItem.IsFolder;// && (projectItem.ResourceTypes == null || projectItem.ResourceTypes.Any(type => m_displayResourcesHS.Contains(type)));
        }

        public override void DragEnter(object[] dragObjects, PointerEventData pointerEventData)
        {
            base.DragEnter(dragObjects, pointerEventData);
            m_treeView.ExternalBeginDrag(pointerEventData.position);
        }

        public override void DragLeave(PointerEventData pointerEventData)
        {
            base.DragLeave(pointerEventData);
            m_treeView.ExternalItemDrop();
            Editor.DragDrop.SetCursor(KnownCursor.DropNowAllowed);
        }

        public override void Drag(object[] dragObjects, PointerEventData pointerEventData)
        {
            base.Drag(dragObjects, pointerEventData);
            m_treeView.ExternalItemDrag(pointerEventData.position);
            if (CanDrop((ProjectItem)m_treeView.DropTarget, dragObjects))
            {
                Editor.DragDrop.SetCursor(KnownCursor.DropAllowed);
            }
            else
            {
                Editor.DragDrop.SetCursor(KnownCursor.DropNowAllowed);
                m_treeView.ClearTarget();
            }
        }

        public override void Drop(object[] dragObjects, PointerEventData pointerEventData)
        {
            base.Drop(dragObjects, pointerEventData);

            ProjectItem dropTarget = (ProjectItem)m_treeView.DropTarget;
            if (CanDrop(dropTarget, dragObjects))
            {
                m_project.Move(dragObjects.OfType<ProjectItem>().ToArray(), dropTarget);
            }
            m_treeView.ExternalItemDrop();
        }
    }
}