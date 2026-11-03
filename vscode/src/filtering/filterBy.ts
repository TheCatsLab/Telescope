/**
 * Port of src/Core/Enums/FilterBy.cs (a [Flags] enum).
 * Indicates the resource node props to consider for filtering.
 */
export const FilterBy = {
    ResourceName: 1 << 0,
    ResourceData: 1 << 1,
    ResourceTagKeys: 1 << 2,
    ResourceTagValues: 1 << 3,
    ResourceEnvironmentVariableKeys: 1 << 4,
    ResourceEnvironmentVariableValues: 1 << 5,
} as const;

export type FilterByOptions = number;

/** A single FilterBy flag value, e.g. FilterBy.ResourceName */
export type FilterByValue = (typeof FilterBy)[keyof typeof FilterBy];

/** Mirrors Enum.HasFlag of the C# original */
export function hasFlag(options: FilterByOptions, flag: FilterByValue): boolean {
    return (options & flag) === flag;
}

/** The default scopes: everything except the (opt-in) Configuration JSON scan */
export const defaultFilterByOptions: FilterByOptions =
    FilterBy.ResourceName
    | FilterBy.ResourceTagKeys
    | FilterBy.ResourceTagValues
    | FilterBy.ResourceEnvironmentVariableKeys
    | FilterBy.ResourceEnvironmentVariableValues;

/** Friendly labels for match counters, port of the FilterMatchViewModel badges */
export const filterByLabels: Readonly<Record<number, string>> = {
    [FilterBy.ResourceName]: "name",
    [FilterBy.ResourceData]: "config",
    [FilterBy.ResourceTagKeys]: "tag keys",
    [FilterBy.ResourceTagValues]: "tag values",
    [FilterBy.ResourceEnvironmentVariableKeys]: "env keys",
    [FilterBy.ResourceEnvironmentVariableValues]: "env values",
};
