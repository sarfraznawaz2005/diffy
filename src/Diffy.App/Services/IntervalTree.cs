using System.Collections.Generic;
using System.Linq;

namespace Diffy.App.Services;

/// <summary>
/// Represents an interval with a start, end, and associated value.
/// </summary>
public class Interval<T>
{
    public int Start { get; set; }
    public int End { get; set; }
    public T Value { get; set; } = default!;

    public bool Overlaps(int start, int end) => Start < end && End > start;
    public bool Contains(int point) => Start <= point && point < End;
}

/// <summary>
/// Node in the Interval Tree.
/// </summary>
public class IntervalTreeNode<T>
{
    public int Center { get; set; }
    public List<Interval<T>> ByStart { get; set; } = new(); // Sorted by start (ascending)
    public List<Interval<T>> ByEnd { get; set; } = new();   // Sorted by end (descending)
    public IntervalTreeNode<T>? Left { get; set; }
    public IntervalTreeNode<T>? Right { get; set; }
}

/// <summary>
/// Interval Tree for efficient overlap queries.
/// Supports O(log n + k) overlap queries where k is the number of results.
/// </summary>
public class IntervalTree<T>
{
    private IntervalTreeNode<T>? _root;
    private readonly int _min;
    private readonly int _max;

    public IntervalTree(List<Interval<T>> intervals)
    {
        if (intervals == null || intervals.Count == 0)
        {
            _root = null;
            _min = 0;
            _max = 0;
            return;
        }

        _min = intervals.Min(i => i.Start);
        _max = intervals.Max(i => i.End);
        _root = BuildTree(intervals);
    }

    private IntervalTreeNode<T> BuildTree(List<Interval<T>> intervals)
    {
        var node = new IntervalTreeNode<T>();

        // Find center point
        var minStart = intervals.Min(i => i.Start);
        var maxEnd = intervals.Max(i => i.End);
        node.Center = (minStart + maxEnd) / 2;

        // Partition intervals
        var leftIntervals = new List<Interval<T>>();
        var rightIntervals = new List<Interval<T>>();
        var centerIntervals = new List<Interval<T>>();

        foreach (var interval in intervals)
        {
            if (interval.End <= node.Center)
                leftIntervals.Add(interval);
            else if (interval.Start > node.Center)
                rightIntervals.Add(interval);
            else
                centerIntervals.Add(interval);
        }

        // Sort center intervals by start (ascending) and end (descending)
        node.ByStart = centerIntervals.OrderBy(i => i.Start).ToList();
        node.ByEnd = centerIntervals.OrderByDescending(i => i.End).ToList();

        // Recursively build subtrees
        if (leftIntervals.Count > 0)
            node.Left = BuildTree(leftIntervals);
        if (rightIntervals.Count > 0)
            node.Right = BuildTree(rightIntervals);

        return node;
    }

    /// <summary>
    /// Finds all intervals that overlap with the given range [start, end).
    /// </summary>
    public List<Interval<T>> Query(int start, int end)
    {
        var result = new List<Interval<T>>();
        if (_root == null) return result;
        Query(_root, start, end, result);
        return result;
    }

    /// <summary>
    /// Finds the interval with the highest priority that contains the given point.
    /// Priority is determined by the order of intervals added (later = higher priority).
    /// </summary>
    public Interval<T>? QueryPoint(int point, int? priorityIndex = null)
    {
        if (_root == null) return null;
        return QueryPoint(_root, point, priorityIndex);
    }

    private void Query(IntervalTreeNode<T> node, int start, int end, List<Interval<T>> result)
    {
        // Query intervals centered at this node
        foreach (var interval in node.ByStart)
        {
            if (interval.Start >= end) break; // No more overlaps possible
            if (interval.Overlaps(start, end))
                result.Add(interval);
        }

        // Query left subtree
        if (node.Left != null && start < node.Center)
            Query(node.Left, start, end, result);

        // Query right subtree
        if (node.Right != null && end > node.Center)
            Query(node.Right, start, end, result);
    }

    private Interval<T>? QueryPoint(IntervalTreeNode<T> node, int point, int? priorityIndex)
    {
        Interval<T>? bestMatch = null;
        int bestPriority = -1;

        // Check intervals centered at this node
        foreach (var interval in node.ByStart)
        {
            if (interval.Contains(point))
            {
                // If priorityIndex provided, use it; otherwise use simple existence check
                // For highlight merging, we want the last matching interval (highest priority)
                int currentPriority = priorityIndex ?? 0;
                if (bestMatch == null || currentPriority >= bestPriority)
                {
                    bestMatch = interval;
                    bestPriority = currentPriority;
                }
            }
        }

        // Query subtrees
        if (point < node.Center && node.Left != null)
        {
            var leftResult = QueryPoint(node.Left, point, priorityIndex);
            if (leftResult != null && bestMatch == null)
                bestMatch = leftResult;
        }
        else if (point >= node.Center && node.Right != null)
        {
            var rightResult = QueryPoint(node.Right, point, priorityIndex);
            if (rightResult != null && bestMatch == null)
                bestMatch = rightResult;
        }

        return bestMatch;
    }

    /// <summary>
    /// Finds all unique split points from all intervals in the tree.
    /// These are the start and end points of all intervals.
    /// </summary>
    public SortedSet<int> GetAllSplitPoints()
    {
        var points = new SortedSet<int>();
        if (_root != null)
            CollectSplitPoints(_root, points);
        return points;
    }

    private void CollectSplitPoints(IntervalTreeNode<T> node, SortedSet<int> points)
    {
        foreach (var interval in node.ByStart)
        {
            points.Add(interval.Start);
            points.Add(interval.End);
        }

        if (node.Left != null)
            CollectSplitPoints(node.Left, points);
        if (node.Right != null)
            CollectSplitPoints(node.Right, points);
    }
}
