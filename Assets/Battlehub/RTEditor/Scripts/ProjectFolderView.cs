﻿using Battlehub.RTCommon;
using Battlehub.RTSaveLoad2.Interface;
using Battlehub.UIControls;
using Battlehub.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Battlehub.RTEditor
{
    public class ProjectFolderView : RuntimeWindow
    {
        public event EventHandler<ProjectTreeEventArgs> ItemDeleted;
        public event EventHandler<ProjectTreeEventArgs> ItemDoubleClick;
        public event EventHandler<ProjectTreeRenamedEventArgs> ItemRenamed;

        private IProject m_project;

        public Type TypeFilter;

        private List<ProjectItem> m_items;
        private ProjectItem[] m_folders;
        public void SetItems(ProjectItem[] folders, ProjectItem[] items, bool reload)
        {
            if(folders == null || items == null)
            {
                m_folders = null;
                m_items = null;
                m_listBox.Items = null;
            }
            else
            {
                m_folders = folders;
                m_items = new List<ProjectItem>(items);
                if (m_items != null)
                {
                    m_items = m_items.Where(item => item.IsFolder).OrderBy(item => item.Name).Union(m_items.Where(item => !item.IsFolder).OrderBy(item => item.Name)).ToList();
                }
                DataBind(reload);
            }
        }

        public void InsertItems(ProjectItem[] items)
        {
            if(m_folders == null)
            {
                return;
            }

            items = items.Where(item => m_folders.Contains(item.Parent)).ToArray();
            if(items.Length == 0)
            {
                return;
            }

            List<ProjectItem> sorted = m_items.Union(items).OrderBy(item => item.Name).Union(m_items.Where(item => !item.IsFolder).OrderBy(item => item.Name)).ToList();
            ProjectItem selectItem = null;
            for(int i = 0; i < sorted.Count; ++i)
            {
                if(items.Contains(sorted[i]))
                {
                    m_listBox.Insert(i, sorted[i]);
                    selectItem = sorted[i];
                }
            }
            m_items = sorted;

            if(selectItem != null)
            {
                m_listBox.SelectedItem = selectItem;
                m_listBox.ScrollIntoView(selectItem);
            }
            
        }


        [SerializeField]
        private GameObject ListBoxPrefab;
        private VirtualizingTreeView m_listBox;
        private bool m_lockSelection;

        public KeyCode RemoveKey = KeyCode.Delete;
        public KeyCode RuntimeModifierKey = KeyCode.LeftControl;
        public KeyCode EditorModifierKey = KeyCode.LeftShift;
        public KeyCode ModifierKey
        {
            get
            {
#if UNITY_EDITOR
                return EditorModifierKey;
#else
                return RuntimeModifierKey;
#endif
            }
        }


        private void DataBind(bool clearItems)
        {
            if (m_items == null)
            {
                m_listBox.SelectedItems = null;
                m_listBox.Items = null;
            }
            else
            {
                if (clearItems)
                {
                    if (m_listBox == null)
                    {
                        Debug.LogError("ListBox is null");
                    }
                    m_listBox.Items = null;
                }

                m_listBox.SelectedItems = null;

                List<ProjectItem> itemsList = m_items.ToList();
                if (TypeFilter != null)
                {
                    for (int i = itemsList.Count - 1; i >= 0; i--)
                    {
                        ProjectItem item = itemsList[i];
                        if (item.IsFolder)
                        {
                            itemsList.Remove(item);
                        }
                        else
                        {
                            AssetItem assetItem = (AssetItem)item;
                            Type type = m_project.ToType(assetItem);
                            if (type == null)
                            {
                                itemsList.RemoveAt(i);
                            }
                            else if (!TypeFilter.IsAssignableFrom(type))
                            {
                                itemsList.RemoveAt(i);
                            }
                        }
                    }

                    if (typeof(GameObject) == TypeFilter)
                    {
                        IEnumerable<GameObject> sceneObjects = Editor.IsPlaying ?
                            ExposeToEditor.FindAll(Editor, ExposeToEditorObjectType.PlayMode) :
                            ExposeToEditor.FindAll(Editor, ExposeToEditorObjectType.EditorMode);

                        foreach (GameObject go in sceneObjects)
                        {
                            AssetItem sceneItem = new AssetItem();
                            sceneItem.ItemID = m_project.ToID(go);
                            sceneItem.Name = go.name;
                            sceneItem.Ext = m_project.GetExt(go);
                            sceneItem.TypeGuid = m_project.ToGuid(typeof(GameObject));
                            itemsList.Add(sceneItem);
                        }
                    }
                    else if (typeof(Component).IsAssignableFrom(TypeFilter))
                    {
                        IEnumerable<GameObject> sceneObjects = Editor.IsPlaying ?
                            ExposeToEditor.FindAll(Editor, ExposeToEditorObjectType.PlayMode) :
                            ExposeToEditor.FindAll(Editor, ExposeToEditorObjectType.EditorMode);

                        foreach (GameObject go in sceneObjects)
                        {
                            Component component = go.GetComponent(TypeFilter);
                            Guid typeGuid = m_project.ToGuid(component.GetType());
                            if (component != null && typeGuid != Guid.Empty)
                            {
                                AssetItem sceneItem = new AssetItem();
                                sceneItem.ItemID = m_project.ToID(go);
                                sceneItem.Name = go.name;
                                sceneItem.Ext = m_project.GetExt(go);
                                sceneItem.TypeGuid = typeGuid;

                                itemsList.Add(sceneItem);
                            }
                        }
                    }

                    //itemsList.Insert(0, none);
                    m_listBox.Items = itemsList;

                }
                else
                {
                    m_listBox.Items = itemsList;
                }


                //if (m_selectedItems != null)
                //{
                //    m_listBox.SelectedItems = SelectionToProjectItemObjectPair(m_selectedItems);
                //}
            }
        }

        protected override void AwakeOverride()
        {
            WindowType = RuntimeWindowType.ProjectFolder;
            base.AwakeOverride();

            if (!ListBoxPrefab)
            {
                Debug.LogError("Set ListBoxPrefab field");
                return;
            }

            m_project = IOC.Resolve<IProject>();

            m_listBox = GetComponentInChildren<VirtualizingTreeView>();
            if (m_listBox == null)
            {
                m_listBox = Instantiate(ListBoxPrefab).GetComponent<VirtualizingTreeView>();
                m_listBox.CanDrag = true;
                m_listBox.CanReorder = false;
                //m_listBox.MultiselectKey = KeyCode.None;
                // m_listBox.RangeselectKey = KeyCode.None;
                //m_listBox.RemoveKey = KeyCode.None;

                m_listBox.CanRemove = false;
                m_listBox.transform.SetParent(transform, false);
            }

            m_listBox.ItemDataBinding += OnItemDataBinding;
            m_listBox.ItemBeginDrag += OnItemBeginDrag;
            m_listBox.ItemDragEnter += OnItemDragEnter;
            m_listBox.ItemDrag += OnItemDrag;
            m_listBox.ItemDragExit += OnItemDragExit;
            m_listBox.ItemDrop += OnItemDrop;
            m_listBox.ItemEndDrag += OnItemEndDrag;
            m_listBox.ItemsRemoving += OnItemRemoving;
            m_listBox.ItemsRemoved += OnItemRemoved;
            m_listBox.ItemDoubleClick += OnItemDoubleClick;
            m_listBox.ItemBeginEdit += OnItemBeginEdit;
            m_listBox.ItemEndEdit += OnItemEndEdit;
        }

        protected override void OnDestroyOverride()
        {
            base.OnDestroyOverride();

            if (m_listBox != null)
            {
                m_listBox.ItemDataBinding -= OnItemDataBinding;
                m_listBox.ItemBeginDrag -= OnItemBeginDrag;
                m_listBox.ItemDragEnter -= OnItemDragEnter;
                m_listBox.ItemDrag -= OnItemDrag;
                m_listBox.ItemDragExit -= OnItemDragExit;
                m_listBox.ItemDrop -= OnItemDrop;
                m_listBox.ItemEndDrag -= OnItemEndDrag;
                m_listBox.ItemsRemoving -= OnItemRemoving;
                m_listBox.ItemsRemoved -= OnItemRemoved;
                m_listBox.ItemDoubleClick -= OnItemDoubleClick;
                m_listBox.ItemBeginEdit -= OnItemBeginEdit;
                m_listBox.ItemEndEdit -= OnItemEndEdit;
            }
        }

        protected override void UpdateOverride()
        {
            base.UpdateOverride();
            if(Editor.Input.GetKeyDown(RemoveKey) && Editor.ActiveWindow == this)
            {
                PopupWindow.Show("Delete Selected Assets", "You cannot undo this action", "Delete", arg =>
                {
                    m_listBox.RemoveSelectedItems();
                },
                "No");
            }
        }

        private void OnItemBeginDrag(object sender, ItemArgs e)
        {
            Editor.DragDrop.RaiseBeginDrag(e.Items, e.PointerEventData);
        }

        private void OnItemDragEnter(object sender, ItemDropCancelArgs e)
        {
            if (e.DropTarget == null || e.DropTarget is AssetItem || e.DragItems != null && e.DragItems.Contains(e.DropTarget))
            {
                Editor.DragDrop.SetCursor(KnownCursor.DropNowAllowed);
                e.Cancel = true;
            }
            else
            {
                Editor.DragDrop.SetCursor(KnownCursor.DropAllowed);
            }
        }

        private void OnItemDrag(object sender, ItemArgs e)
        {
            Editor.DragDrop.RaiseDrag(e.PointerEventData);
        }

        private void OnItemDragExit(object sender, EventArgs e)
        {
            Editor.DragDrop.SetCursor(KnownCursor.DropNowAllowed);
        }

        private void OnItemDrop(object sender, ItemDropArgs e)
        {
            Editor.DragDrop.RaiseDrop(e.PointerEventData);

            if (!(e.DropTarget is AssetItem) && (e.DragItems == null || !e.DragItems.Contains(e.DropTarget)))
            {
                ProjectItem[] projectItems = e.DragItems.OfType<ProjectItem>().ToArray();
                m_project.Move(projectItems, (ProjectItem)e.DropTarget);
            }
        }

        private void OnItemEndDrag(object sender, ItemArgs e)
        {
            Editor.DragDrop.RaiseDrop(e.PointerEventData);

            foreach (ProjectItem item in e.Items)
            {
                if (m_folders.All(f => f.Children == null || !f.Children.Contains(item)))
                {
                    m_listBox.RemoveChild(item.Parent, item, item.Parent.Children.Count == 1);
                }
            }
        }

        private void OnItemDataBinding(object sender, ItemDataBindingArgs e)
        {
            ProjectItem projectItem = e.Item as ProjectItem;
            if (projectItem == null)
            {
                Text text = e.ItemPresenter.GetComponentInChildren<Text>(true);
                text.text = null;
                ProjectItemView itemView = e.ItemPresenter.GetComponentInChildren<ProjectItemView>(true);
                itemView.ProjectItem = null;
            }
            else
            {
                Text text = e.ItemPresenter.GetComponentInChildren<Text>(true);
                text.text = projectItem.Name;
                ProjectItemView itemView = e.ItemPresenter.GetComponentInChildren<ProjectItemView>(true);
                itemView.ProjectItem = projectItem;
            }
        }

        private void OnItemRemoving(object sender, ItemsCancelArgs e)
        {
            
        }

        private void OnItemRemoved(object sender, ItemsRemovedArgs e)
        {
            for(int i = 0; i < e.Items.Length; ++i)
            {
                m_items.Remove((ProjectItem)e.Items[i]);
            }

            if (ItemDeleted != null)
            {
                ItemDeleted(this, new ProjectTreeEventArgs(e.Items.OfType<ProjectItem>().ToArray()));
            }
        }

        private void OnItemDoubleClick(object sender, ItemArgs e)
        {
            if(ItemDoubleClick != null)
            {
                ItemDoubleClick(this, new ProjectTreeEventArgs(e.Items.OfType<ProjectItem>().ToArray()));
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

                Image itemImage = e.ItemPresenter.GetComponentInChildren<Image>(true);
                Image image = e.EditorPresenter.GetComponentInChildren<Image>(true);
                image.sprite = itemImage.sprite;
                image.gameObject.SetActive(true);

                
                Text text = e.ItemPresenter.GetComponentInChildren<Text>(true);
                text.text = item.Name;

                LayoutElement layout = inputField.GetComponent<LayoutElement>();
                if(layout != null)
                {
                    RectTransform rt = text.GetComponent<RectTransform>();
                    layout.preferredWidth = rt.rect.width;
                }
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

            if (projectItem.Name != oldName)
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
    }
}
