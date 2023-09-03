using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using NUnit.Framework;

namespace UnitTests
{
    public class TopoSortTest
    {
        [Test]
        public void test()
        {
            Assert.AreEqual(
                TopoSort.DoSort(new int[0], new (int, int)[0]),
                new int[0].ToList()
            );
            
            Assert.AreEqual(
                TopoSort.DoSort(new List<int> {1}, new List<(int, int)>()),
                new List<int> { 1 }
            );
            
            Assert.AreEqual(
                TopoSort.DoSort(new List<int> {1, 2}, new List<(int, int)>()),
                new List<int> { 1, 2 }
            );
            
            Assert.AreEqual(
                TopoSort.DoSort(new List<int> {1,2}, new List<(int, int)>() { (2, 1) }),
                new List<int> { 2, 1 }
            );
            
            Assert.AreEqual(
                TopoSort.DoSort(new List<int> {2,1}, new List<(int, int)>() { }),
                new List<int> { 2, 1 }
            );
            
            Assert.AreEqual(
                TopoSort.DoSort(new List<int> {1,2,3}, new List<(int, int)>() { (2, 1) }),
                new List<int> { 2, 1, 3 }
            );
            
            Assert.AreEqual(
                TopoSort.DoSort(new List<int> {1,2,3}, new List<(int, int)>() { (2, 1), (3, 2) }),
                new List<int> { 3, 2, 1 }
            );
            
            Assert.AreEqual(
                TopoSort.DoSort(new List<int> {1,2,3}, new List<(int, int)>() { (2, 1), (3, 2), (3, 1) }),
                new List<int> { 3, 2, 1 }
            );
            
            Assert.Throws<Exception>(() => 
                TopoSort.DoSort(new List<int> {1,2,3}, new List<(int, int)>() { (2, 1), (3, 2), (1, 3) })
            );

            Assert.AreEqual(
                TopoSort.DoSort(new List<int> {1, 2, 3, 4, 5}, new List<(int, int)>() {(2, 1), (3, 2), (4, 3), (5, 4)}),
                new List<int> {5, 4, 3, 2, 1}
            );
        }
    }
}