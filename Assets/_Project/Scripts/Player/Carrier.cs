using System;
using System.Collections.Generic;
using UnityEngine;

namespace APProto
{
    // 손에 든 택배 목록. HandAnchor 아래 PackageStack 으로 표시. (플레이어/알바 공용)
    public class Carrier : MonoBehaviour
    {
        [SerializeField] PackageStack handStack;

        public readonly List<Package> Items = new();
        public int Capacity { get; set; }
        public int Count => Items.Count;
        public bool IsFull => Items.Count >= Capacity;

        public int FindIndex(Predicate<Package> match) => Items.FindIndex(match);

        // 카트를 밀 때는 카트 적재함에 표시. null 이면 원래 손으로.
        PackageStack hand;
        public void SetDisplay(PackageStack stack)
        {
            hand ??= handStack;
            handStack.Show(Array.Empty<Package>());
            handStack = stack ? stack : hand;
            handStack.Show(Items);
        }

        public void Add(Package pkg)
        {
            Items.Add(pkg);
            handStack.Show(Items);
        }

        public Package RemoveAt(int index)
        {
            var pkg = Items[index];
            Items.RemoveAt(index);
            handStack.Show(Items);
            return pkg;
        }
    }
}
