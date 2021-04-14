﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

public class Laters : EditorWindow, ISerializationCallbackReceiver, IHasCustomMenu
{
    public static Laters Instance { get; private set; }
    static readonly int SIZE = 30, ITEM_SIZE = 32, PADDING = 1, ITEM_PADDED = 33;
    bool initialized;
    [SerializeField] Item[] cache;
    Queue<Item> markedObjects;
    Stack<Item> pool;

    [MenuItem("Window/BAStudio/Laters")]
    public static void ShowWindow()
    {
        if (Instance != null)
        {
            Instance.ShowNotification(new GUIContent("I'm here!"));
            return;
        }
        Instance = EditorWindow.GetWindow<Laters>();
    }

    GUIStyle styleAvailable, styleUnavailable;
    GUIContent outOfScope = new GUIContent("Selected object\nis out of scope!");
    GUIContent full = new GUIContent("Storage full!\nClear the list for\nbest productivity👊");
    bool latersEnabled;

    void OnEnable ()
    {
        if (Instance == null) Instance = this;
        initialized = false;
    }

    [SerializeField] Vector2 scroll;

    void OnGUI ()
    {
        if (!initialized) Initialize();

        if (styleAvailable == null)
        {
            styleAvailable = new GUIStyle(GUI.skin.button);
            styleAvailable.fontSize = 16;
            styleAvailable.alignment = TextAnchor.MiddleLeft;
        }
        if (styleUnavailable == null)
        {
            styleUnavailable = new GUIStyle(GUI.skin.button);
            styleUnavailable.normal.textColor = Color.grey;
            styleUnavailable.alignment = TextAnchor.MiddleLeft;
            styleUnavailable.fontSize = 12;
        }

        bool repaint = false;

		if (Event.current.type == EventType.DragUpdated)
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
			Event.current.Use();
		}
        else if (Event.current.type == EventType.DragPerform)
        {
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            {
                if (!AddItem(DragAndDrop.objectReferences[i], true)) break;
            }

            DragAndDrop.AcceptDrag();
            Event.current.Use();

            repaint = true;
            // Repaint();
            return;
        }

        EditorGUIUtility.SetIconSize(new Vector2(24f, 24f));
        if (markedObjects.Count == 0) return;

        int size, index;
        size = markedObjects.Count;
        index = -1;

        using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll, GUIStyle.none, GUI.skin.verticalScrollbar))
        {
            scroll = scrollView.scrollPosition;

            // Optimization: Draw only visible control and compress all invisible control to 2 rects (upper/lower)
            // Calculate upper/lower palceholder height
            float upperBound = scroll.y;
            float lowerBound = scroll.y + position.height;
            int firstVisibleIndex = (int) Mathf.Floor((upperBound - ITEM_SIZE) / (float) ITEM_PADDED) + 1;
            int lastVisibleIndex = (int) Mathf.Ceil(lowerBound / (float) ITEM_PADDED);

            GUILayoutUtility.GetRect(position.width, (firstVisibleIndex - 1) * ITEM_PADDED);

            using (var e = markedObjects.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    index++;
                    if (index < firstVisibleIndex || index > lastVisibleIndex)
                    {
                        continue;
                    }

                    Rect r = GUILayoutUtility.GetRect(position.width, ITEM_SIZE);
                    GUILayout.Space(1);

                    bool available = true;
                    if (e.Current.obj == null) available = false;
                    if (e.Current.guiContent == null)
                        e.Current.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(e.Current.obj, null));

                    if (GUI.Button(r, e.Current.guiContent, available? styleAvailable : styleUnavailable))
                    {
                        if (Event.current.button == (int) MouseButton.RightMouse)
                        {
                            if (available)
                            {
                                if (Forevers.Instance == null)
                                    Forevers.ShowWindow();
                                Forevers.Instance.AddItem(e.Current.obj);
                            }
                            else ShowNotification(outOfScope);
                        }
                        else
                        {
                            if (available)
                            {
                                Selection.SetActiveObjectWithContext(e.Current.obj, null);
                                selectingWithin = e.Current.obj;
                            }
                            else ShowNotification(outOfScope);
                        }
                    }
                }
            }

            GUILayoutUtility.GetRect(position.width, (markedObjects.Count - 1 - lastVisibleIndex) * ITEM_PADDED);
        }

        if (repaint)
        {
            UpdateCount();
            Repaint();
        }
    }

    public bool AddItem (UnityEngine.Object obj, bool delayRepaint = false)
    {
        Item item = GetItemFromPool();
        item.obj = obj;
        item.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(obj, null));
        this.markedObjects.Enqueue(item);
        Item i = GetItemFromPool();
        if (markedObjects.Count >= SIZE) pool.Push(markedObjects.Dequeue());
        UpdateCount();
        if (!delayRepaint)
            Repaint();
        return true;
    }

    void UpdateCount ()
    {
        this.titleContent.text = string.Concat("Laters ", markedObjects.Count, "/", SIZE);
    }

    public void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(new GUIContent("Clear"), false, Clear);
    }

    public void Clear ()
    {
        markedObjects.Clear();
        UpdateCount();
    }

    void Initialize()
    {
        if (markedObjects == null) markedObjects = new Queue<Item>();
        pool = new Stack<Item>(SIZE);
        initialized = true;
    }

    Item GetItemFromPool ()
    {
        if (pool.Count > 0) return pool.Pop();
        else return new Item();
    }

    UnityEngine.Object selectingWithin;

    public void OnBeforeSerialize()
    {
        if (cache == null) cache = new Item[SIZE];
        else
        {
            for (int i = 0; i < cache.Length; i++) cache[i] = null;
        }
        int index = 0;
        using (var e = markedObjects.GetEnumerator())
        {
            while (e.MoveNext())
            {
                cache[index] = e.Current;
                index++;
            }
        }
    }

    public void OnAfterDeserialize()
    {
        if (markedObjects == null) markedObjects = new Queue<Item>();
        else markedObjects.Clear();

        if (cache == null) return;

        for (int i = 0; i < cache.Length; i++)
        {
            if (cache[i] == null || cache[i].obj == null) continue;
            markedObjects.Enqueue(cache[i]);
        }
        UpdateCount();
    }

    [Serializable]
    class Item
    {
        public UnityEngine.Object obj;
        public GUIContent guiContent;
    }
}
