using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

public class Laters : EditorWindow, ISerializationCallbackReceiver
{
    static readonly int SIZE = 30;
    bool initialized;
    [SerializeField] Item[] cache;
    HashSet<Item> markedObjects;
    Stack<Item> pool;

    [MenuItem("Window/BAStudio/Laters")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow<Laters>();
    }

    GUIStyle styleAvailable, styleUnavailable;
    GUIContent outOfScope = new GUIContent("Selected object\nis out of scope!");
    GUIContent full = new GUIContent("Storage full! Clear the list for better productivity. Max: " + SIZE);
    bool latersEnabled;

    void OnEnable ()
    {
        initialized = false;
    }

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

        Rect rect;
        int size, index;
        size = markedObjects.Count;
        index = 0;

        using (var e = markedObjects.GetEnumerator())
        {
            while (e.MoveNext())
            {
                int y = index * 33;
                if (y > position.height)
                {
                    index++;
                    continue;
                }

                bool available = true;
                if (e.Current.obj == null) available = false;
                if (e.Current.guiContent == null)
                    e.Current.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(UnityEditor.Selection.activeObject, null));

                rect = new Rect(0, y, position.width, 32);
                if (GUI.Button(rect, e.Current.guiContent, available? styleAvailable : styleUnavailable))
                {
                    if (Event.current.button == (int) MouseButton.RightMouse)
                    {
                        markedObjects.Remove(e.Current);
                        repaint = true;
                        break;
                    }
                    if (available)
                    {
                        Selection.SetActiveObjectWithContext(e.Current.obj, null);
                        selectingWithin = e.Current.obj;
                    }
                    else ShowNotification(outOfScope);
                }
                index++;
            }
        }

        if (repaint)
        {
            UpdateCount();
            Repaint();
        }
    }

    public bool AddItem (UnityEngine.Object obj, bool delayRepaint = false)
    {
        if (markedObjects.Count >= SIZE)
        {
            ShowNotification(full);
            return false;
        }
        Item item = GetItemFromPool();
        item.obj = obj;
        item.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(obj, null));
        this.markedObjects.Add(item);
        UpdateCount();
        if (!delayRepaint)
            Repaint();
        return true;
    }

    void UpdateCount ()
    {
        this.titleContent.text = string.Concat("Laters ", markedObjects.Count, "/", SIZE);
    }

    void Initialize()
    {
        if (markedObjects == null) markedObjects = new HashSet<Item>();
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
        if (markedObjects == null) markedObjects = new HashSet<Item>();
        else markedObjects.Clear();

        if (cache == null) return;

        for (int i = 0; i < cache.Length; i++)
        {
            if (cache[i] == null || cache[i].obj == null) continue;
            markedObjects.Add(cache[i]);
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
