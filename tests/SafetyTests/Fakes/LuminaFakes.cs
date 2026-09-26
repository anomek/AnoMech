// Stand-ins for the Lumina sheet API. Rows come from VirtualGame's tables; the sheet and row-ref
// semantics that ZoneSession relies on match Lumina's (a missing row throws from the indexer,
// RowRef.Value throws on an invalid reference).
using System;
using System.Collections.Generic;
using AnoMech.SafetyTests.Harness;

namespace Lumina.Excel
{
    public sealed class ExcelSheet<T> where T : struct
    {
        private readonly IReadOnlyDictionary<uint, T> rows;

        internal ExcelSheet(IReadOnlyDictionary<uint, T> rows) => this.rows = rows;

        public T this[uint rowId] => rows.TryGetValue(rowId, out var row)
            ? row
            : throw new ArgumentOutOfRangeException(nameof(rowId), $"{typeof(T).Name} has no row {rowId}");

        public T? GetRowOrDefault(uint rowId) => rows.TryGetValue(rowId, out var row) ? row : null;

        public bool TryGetRow(uint rowId, out T row) => rows.TryGetValue(rowId, out row);

        public bool HasRow(uint rowId) => rows.ContainsKey(rowId);
    }

    public readonly struct RowRef
    {
        public RowRef(uint rowId) => RowId = rowId;

        public uint RowId { get; }
    }

    public readonly struct RowRef<T> where T : struct
    {
        public RowRef(uint rowId) => RowId = rowId;

        public uint RowId { get; }
        public bool IsValid => VirtualGame.Current.Sheet<T>().HasRow(RowId);
        public T? ValueNullable => VirtualGame.Current.Sheet<T>().GetRowOrDefault(RowId);
        public T Value => ValueNullable ?? throw new InvalidOperationException($"{typeof(T).Name} row {RowId} is not valid");
    }

    public sealed class Collection<T>
    {
        private readonly T[] items;

        public Collection(params T[] items) => this.items = items;

        public int Count => items.Length;
        public T this[int index] => items[index];
        public Enumerator GetEnumerator() => new(items);

        public struct Enumerator
        {
            private readonly T[] items;
            private int index;

            internal Enumerator(T[] items)
            {
                this.items = items;
                index = -1;
            }

            public T Current => items[index];

            public bool MoveNext() => ++index < items.Length;
        }
    }
}

namespace Lumina.Text.ReadOnly
{
    public readonly struct ReadOnlySeString
    {
        private readonly string? text;

        public ReadOnlySeString(string text) => this.text = text;

        public override string ToString() => text ?? "";
    }
}

namespace Lumina.Excel.Sheets
{
    using Lumina.Excel;

    public struct TerritoryIntendedUse
    {
    }

    public struct InstanceContent
    {
    }

    public struct TerritoryType
    {
        public RowRef<TerritoryIntendedUse> TerritoryIntendedUse { get; init; }
        public RowRef<ContentFinderCondition> ContentFinderCondition { get; init; }
    }

    public struct ContentFinderCondition
    {
        public RowRef Content { get; init; }
        public byte ClassJobLevelSync { get; init; }
        public ushort ItemLevelSync { get; init; }
        public ushort ItemLevelRequired { get; init; }
    }

    public struct ClassJob
    {
    }

    public struct BaseParam
    {
    }

    public struct ItemLevel
    {
    }

    public struct EquipSlotCategory
    {
    }

    public struct Item
    {
        public Lumina.Text.ReadOnly.ReadOnlySeString Name { get; init; }
        public RowRef<ItemLevel> LevelItem { get; init; }
        public Collection<RowRef<BaseParam>> BaseParam { get; init; }
        public Collection<short> BaseParamValue { get; init; }
        public byte BaseParamModifier { get; init; }
        public RowRef<EquipSlotCategory> EquipSlotCategory { get; init; }
    }

    public struct ParamGrow
    {
        public int BaseSpeed { get; init; }
    }

    public struct ResistanceWeaponAdjust
    {
    }

    public struct Materia
    {
        public Collection<short> Value { get; init; }
        public RowRef<BaseParam> BaseParam { get; init; }
    }

    public struct MandervilleWeaponEnhance
    {
    }

    public struct ItemFood
    {
        public Collection<ParamsStruct> Params { get; init; }

        public struct ParamsStruct
        {
            public RowRef<BaseParam> BaseParam { get; init; }
            public bool IsRelative { get; init; }
            public sbyte Value { get; init; }
            public sbyte ValueHQ { get; init; }
            public short Max { get; init; }
            public short MaxHQ { get; init; }
        }
    }
}
