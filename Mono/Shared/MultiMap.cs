using System;
using System.Collections.Generic;
using System.Collections;

/**
Dictionary with non unique keys.

@tparam K   Type of the key.
@tparam V   Type of the value.
**/
public class MultiValueDictionary<K, V> : IEnumerable<KeyValuePair<K, V>>
{
    // TODO: Use HashSet instead of List?
    private Dictionary<K, List<V>> _dictionary = new Dictionary<K, List<V>>();

    /**
    Gets all keys as an IEnumerable.
    
    @return The keys.
    **/
    public IEnumerable<K> Keys
    {
        get
        {
            return _dictionary.Keys;
        }
    }

    /**
    Gets all vales as an IEnumerable.
    
    @return The values.
    **/
    public IEnumerable<List<V>> Values
    {
        get
        {
            return _dictionary.Values;
        }
    }

    /**
    Adds a key and value to the dictionary.
    
    @param  key     The key of the value.
    @param  value   The value to add.
    **/
    public void Add(K key, V value)
    {
        List<V> list;
        if(this._dictionary.TryGetValue(key, out list))
        {
            list.Add(value);
        }
        else
        {
            list = new List<V>();
            list.Add(value);
            _dictionary[key] = list;
        }
    }

    /**
    Adds a key value pair to the dictionary.
    
    @param  pair    The key value pair to add.
    **/
    public void Add(KeyValuePair<K, V> pair)
    {
        Add(pair.Key, pair.Value);
    }

    /**
    Adds a key value pair to the dictionary. Throws exception if given object is not a KeyValuePair<K, V>.
    
    @param  obj The Object to add, must be a KeyValuePair<K, V>.
    **/
    public void Add(Object obj)
    {
        Add((KeyValuePair<K, V>)obj);
    }

    /**
    Removes all values with given key from the dictionary.
    
    @param  key The key for which all values must be removed.
    **/
    public void Remove(K key)
    {
        _dictionary.Remove(key);
    }

    /**
    Removes a certain key and value from the dictionary.
    
    @param  key     The key of the value.
    @param  value   The value to remove.
    **/
    public void Remove(K key, V value)
    {
        List<V> list;
        if(_dictionary.TryGetValue(key, out list))
        {
            list.Remove(value);
        }
    }

    /**
    Tries to get all values for a given key.
    
    @param  key             The key to search for.
    @param [out]    list    The list of values found for given key.
    
    @return True if values for key were found, false if not.
    **/
    public bool TryGetValue(K key, out List<V> list)
    {
        return _dictionary.TryGetValue(key, out list);
    }

    /**
    Indexer on dictionary keys.
    
    @return The indexed items.
    **/
    public List<V> this[K key]
    {
        get
        {
            List<V> list;
            if(_dictionary.TryGetValue(key, out list))
            {
                return list;
            }
            else
            {
                return new List<V>();
            }
        }
    }

    /**
    Returns an enumerator over every key-value pair.
    
    @return The enumerator.
    **/
    public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
    {
        foreach(KeyValuePair<K, List<V>> pair in _dictionary)
        {
            foreach(V val in pair.Value)
            {
                yield return new KeyValuePair<K, V>(pair.Key, val);
            }
        }
    }

    /**
    Returns an enumerator over every key-value pair.
    
    @return The enumerator.
    **/
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}