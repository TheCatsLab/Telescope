import { FilterByValue } from "./filterBy";

/** Presents a match of a filter by {@link by} {@link count} times; port of the C# Match record */
export interface FilterMatch {
    by: FilterByValue;
    count: number;
}

/** Port of src/Core/Models/NodeFilterResult.cs */
export class NodeFilterResult {
    public readonly matches: FilterMatch[] = [];

    public get success(): boolean {
        return this.matches.some((match) => match.count > 0);
    }
}
