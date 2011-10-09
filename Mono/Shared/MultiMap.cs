using System;
using System.Collections.Generic;

public class MultiValueDictionary<K, V> {
    // TODO: Use HashSet instead of List?
    Dictionary<K, List<V>> _dictionary = new Dictionary<K, List<V>>();

    public void Add(K key, V value) {
        List<V> list;
        if(this._dictionary.TryGetValue(key, out list)) {
            list.Add(value);
        } else {
            list = new List<V>();
            list.Add(value);
            _dictionary[key] = list;
        }
    }

    public void Remove(K key) {
        _dictionary.Remove(key);
    }

    public void Remove(K key, V value) {
        List<V> list;
        if(_dictionary.TryGetValue(key, out list)) {
            list.Remove(value);
        }
    }

    public bool TryGetValue(K key, out List<V> list) {
        return _dictionary.TryGetValue(key, out list);
    }

    public IEnumerable<K> Keys {
        get {
            return _dictionary.Keys;
        }
    }

    public List<V> this[K key] {
        get {
            List<V> list;
            if(_dictionary.TryGetValue(key, out list)) {
                return list;
            } else {
                return new List<V>();
            }
        }
    }
}