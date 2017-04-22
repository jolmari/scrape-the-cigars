using System;
using System.Collections.Generic;
using System.Linq;

namespace CigarInventoryCrawler
{
    public static class EnumerableExtensions
    {
        public static T PickRandomElement<T>(this IEnumerable<T> collection)
        {
            return collection.ElementAt(new Random().Next(0, collection.Count()));
        }

        public static IEnumerable<IEnumerable<T>> Chunkify<T>(this IEnumerable<T> collection, int chunkSize)
        {
            return collection
                .Select((item, index) => new { item, index})
                .GroupBy(item => item.index / chunkSize)
                .Select(group => group.Select(member => member.item));
        }
    }
}
