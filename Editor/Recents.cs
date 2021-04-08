using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Recents : EditorWindow, ISerializationCallbackReceiver
{
    bool initialized;
    [SerializeField] Item[] cache;
    Queue<Item> recentObjects;
    Stack<Item> pool;

    [MenuItem("Window/Recents")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(Recents));
    }

    void OnEnable ()
    {
        initialized = false;
    }

    void OnGUI ()
    {
        if (!initialized) Initialize();

        EditorGUIUtility.SetIconSize(new Vector2(24f, 24f));
        if (recentObjects.Count == 0) return;

        Rect rect;
        int size, index;
        size = recentObjects.Count;
        index = 0;

        using (var e = recentObjects.GetEnumerator())
        {
            while (e.MoveNext())
            {
                rect = new Rect(0, (size - index - 1) * 36, position.width, 32);
                if (GUI.Button(rect, e.Current.guiContent))
                    Selection.SetActiveObjectWithContext(e.Current.obj, null);
                index++;
            }
        }
    }

    void Initialize()
    {
        if (recentObjects == null) recentObjects = new Queue<Item>();
        pool = new Stack<Item>(50);
        initialized = true;
    }

    Item GetItemFromPool ()
    {
        if (pool.Count > 0) return pool.Pop();
        else return new Item();
    }

    void OnSelectionChange()
    {
        if (UnityEditor.Selection.activeObject == null) return;
        Item i = GetItemFromPool();
        i.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(UnityEditor.Selection.activeObject, null));;
        i.obj = UnityEditor.Selection.activeObject;
        recentObjects.Enqueue(i);
        if (recentObjects.Count > 50) pool.Push(recentObjects.Dequeue());
        Repaint();
    }
    public void OnBeforeSerialize()
    {
        if (cache == null) cache = new Item[50];
        int index = 0;
        using (var e = recentObjects.GetEnumerator())
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
        if (recentObjects == null) recentObjects = new Queue<Item>(50);
        else recentObjects.Clear();

        if (cache == null) return;

        for (int i = 0; i < cache.Length; i++)
        {
            if (cache[i] != null) recentObjects.Enqueue(cache[i]);
            else break;
        }
    }

    [Serializable]
    class Item
    {
        public UnityEngine.Object obj;
        public GUIContent guiContent;
    }
}
