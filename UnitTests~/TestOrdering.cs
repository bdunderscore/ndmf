using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.model;
using NUnit.Framework;

namespace UnitTests
{
    public class TopoSortTest
    {
        private List<int> DoWeakSort(IEnumerable<int> items, IEnumerable<(int, int)> constraints)
        {
            return TopoSort.DoSort<int>(items, constraints.Select(x => (x.Item1, x.Item2, ConstraintType.WeakOrder)));
        }
        
        [Test]
        public void WeakOrderingTest()
        {
            Assert.AreEqual(
                DoWeakSort(new int[0], new (int, int)[0]),
                new int[0].ToList()
            );
            
            Assert.AreEqual(
                DoWeakSort(new List<int> {1}, new List<(int, int)>()),
                new List<int> { 1 }
            );
            
            Assert.AreEqual(
                DoWeakSort(new List<int> {1, 2}, new List<(int, int)>()),
                new List<int> { 1, 2 }
            );
            
            Assert.AreEqual(
                DoWeakSort(new List<int> {1,2}, new List<(int, int)>() { (2, 1) }),
                new List<int> { 2, 1 }
            );
            
            Assert.AreEqual(
                DoWeakSort(new List<int> {2,1}, new List<(int, int)>() { }),
                new List<int> { 2, 1 }
            );
            
            Assert.AreEqual(
                DoWeakSort(new List<int> {1,2,3}, new List<(int, int)>() { (2, 1) }),
                new List<int> { 2, 1, 3 }
            );
            
            Assert.AreEqual(
                DoWeakSort(new List<int> {1,2,3}, new List<(int, int)>() { (2, 1), (3, 2) }),
                new List<int> { 3, 2, 1 }
            );
            
            Assert.AreEqual(
                DoWeakSort(new List<int> {1,2,3}, new List<(int, int)>() { (2, 1), (3, 2), (3, 1) }),
                new List<int> { 3, 2, 1 }
            );
            
            Assert.Throws<Exception>(() => 
                DoWeakSort(new List<int> {1,2,3}, new List<(int, int)>() { (2, 1), (3, 2), (1, 3) })
            );

            Assert.AreEqual(
                DoWeakSort(new List<int> {1, 2, 3, 4, 5}, new List<(int, int)>() {(2, 1), (3, 2), (4, 3), (5, 4)}),
                new List<int> {5, 4, 3, 2, 1}
            );
        }

        [Test]
        public void StrongSequencingTest()
        {
            Assert.AreEqual(
                new [] {1,3,2},
                TopoSort.DoSort(
                    new []
                    {
                        1, 2, 3
                    }, new []
                    {
                        (1,3,ConstraintType.Sequence)
                    })
            );
            
            Assert.AreEqual(
                new [] {1,4,3,2},
                TopoSort.DoSort(
                    new []
                    {
                        1, 2, 3, 4
                    }, new []
                    {
                        (1,3,ConstraintType.Sequence),
                        (1,4,ConstraintType.WaitFor),
                        (1,2,ConstraintType.WeakOrder)
                    })
            );
            
            Assert.AreEqual(
                new [] {1,2,4,3},
                TopoSort.DoSort(
                    new []
                    {
                        1, 2, 3, 4
                    }, new []
                    {
                        (1,3,ConstraintType.Sequence),
                        (1,4,ConstraintType.WaitFor),
                        (1,2,ConstraintType.WaitFor)
                    })
            );
            
            Assert.AreEqual(
                new [] {1,4,2,3},
                TopoSort.DoSort(
                    new []
                    {
                        1, 2, 3, 4
                    }, new []
                    {
                        (1,3,ConstraintType.WaitFor),
                        (2,3,ConstraintType.WeakOrder),
                        (1,4,ConstraintType.WaitFor)
                    })
            );
            
            Assert.AreEqual(
                new [] {1,4,2,3},
                TopoSort.DoSort(
                    new []
                    {
                        1, 2, 3, 4
                    }, new []
                    {
                        (1,3,ConstraintType.Sequence),
                        (2,3,ConstraintType.WeakOrder),
                        (1,4,ConstraintType.Sequence)
                    })
            );
        }
    }
}