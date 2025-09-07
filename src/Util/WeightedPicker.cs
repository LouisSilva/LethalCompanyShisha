using System;
using System.Collections.Generic;
using System.Linq;

namespace LethalCompanyShisha.Util;

public class WeightedPicker<T>
{
    private readonly List<(T item, float cumulativeWeight)> _processedItems;
    private readonly float _totalWeight;

    /// <summary>
    /// Creates a new weighted picker instance.
    /// </summary>
    /// <param name="weightedItems">A list of items and their associated weights.</param>
    public WeightedPicker(IEnumerable<(T item, float weight)> weightedItems)
    {
        List<(T item, float weight)> items = weightedItems.ToList();
        if (items.Count == 0)
        {
            throw new ArgumentException("Item list cannot be null or empty.");
        }

        _processedItems = new List<(T, float)>(items.Count);
        float currentCumulativeWeight = 0f;

        foreach ((T item, float weight) in items)
        {
            if (weight < 0f)
            {
                throw new ArgumentException("Weights must be non-negative.");
            }
            currentCumulativeWeight += weight;
            _processedItems.Add((item, currentCumulativeWeight));
        }

        _totalWeight = currentCumulativeWeight;

        if (_totalWeight <= 0f)
        {
            throw new ArgumentException("Total weight of all items must be greater than 0.");
        }
    }

    /// <summary>
    /// Picks a random item based on the pre-calculated weights.
    /// </summary>
    /// <returns>A randomly selected item of type T.</returns>
    public T PickOne()
    {
        float roll = UnityEngine.Random.Range(0f, _totalWeight);

        foreach ((T item, float cumulativeWeight) in _processedItems)
        {
            if (roll < cumulativeWeight)
            {
                return item;
            }
        }

        // Fallback for floating point precision issues
        return _processedItems[_processedItems.Count - 1].item;
    }
}