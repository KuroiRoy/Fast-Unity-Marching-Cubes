﻿using System.Collections.Generic;

namespace SkywardRay.Utility {

public class Pool<TItem> {

    /// <summary>
    /// Use this delegate to specify how a new item should be created
    /// </summary>
    public delegate TItem NewItem ();

    /// <summary>
    /// Use this delegate to specify how an existing item should be prepared 
    /// </summary>
    public delegate void ResetItem (ref TItem item);

    private readonly Queue<TItem> items;

    public Pool (int initialCapacity = 200) {
        items = new Queue<TItem>(initialCapacity);
    }

    public void AddItem (TItem item) {
        items.Enqueue(item);
    }

    public TItem GetItem (NewItem newItem, ResetItem resetItem) {
        TItem item;

        if (items.Count > 0) {
            item = items.Dequeue();
        }
        else {
            item = newItem.Invoke();
        }
        
        resetItem.Invoke(ref item);
        
        return item;
    }

}

}