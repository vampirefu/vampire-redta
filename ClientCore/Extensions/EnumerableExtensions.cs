using System.Collections.Generic;
using System.Linq;

namespace ClientCore.Extensions;

public static class EnumerableExtensions
{
    /// <summary>
    /// 将可枚举集合转换为矩阵，每列有最大项目数限制。
    /// 矩阵按列从左到右构建。
    /// </summary>
    /// <param name="enumerable">要转换的可枚举集合</param>
    /// <param name="maxPerColumn">每列的最大项目数</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static List<List<T>> ToMatrix<T>(this IEnumerable<T> enumerable, int maxPerColumn)
    {
        var list = enumerable.ToList();
        return list.Aggregate(new List<List<T>>(), (matrix, item) =>
        {
            int index = list.IndexOf(item);
            int column = (index / maxPerColumn);
            List<T> columnList = matrix.Count <= column ? new List<T>() : matrix[column];
            if (columnList.Count == 0)
                matrix.Add(columnList);

            columnList.Add(item);
            return matrix;
        });
    }
}