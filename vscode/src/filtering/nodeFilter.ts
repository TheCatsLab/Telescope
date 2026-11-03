import { FilterByOptions } from "./filterBy";

/**
 * Regex metacharacters escaped as literal text; the port of .NET Regex.Escape
 * for the characters that matter in JS. Like .NET, a lone "}" is left as is -
 * it is a literal in a JS pattern as well.
 */
function escapeRegExp(text: string): string {
    return text.replace(/[-.*+?^${()|[\]\\]/g, "\\$&");
}

/**
 * Presents the resource nodes tree filtering options.
 * Port of src/Core/Models/NodeFilter.cs.
 */
export class NodeFilter {
    /** Text to filter the nodes by; null/undefined means "no filter" */
    public searchText: string | null | undefined;

    /** Indicates the node props to consider for filtering (a FilterBy bit set) */
    public filterByOptions: FilterByOptions;

    /** Port of the C# StringComparison: the matcher only distinguishes Ordinal(IgnoreCase) */
    public ignoreCase: boolean;

    /**
     * Indicates if the search text is a regular expression;
     * by default it is treated as literal text
     */
    public isRegex: boolean;

    /**
     * Computed lazily like the C# original: null cache means "not computed yet",
     * so a null result (empty search) is simply recomputed on the next access.
     */
    private cachedEffectiveSearchText: string | null | undefined;

    constructor(init?: Partial<Pick<NodeFilter, "searchText" | "filterByOptions" | "ignoreCase" | "isRegex">>) {
        this.searchText = init?.searchText;
        this.filterByOptions = init?.filterByOptions ?? 0;
        this.ignoreCase = init?.ignoreCase ?? true;
        this.isRegex = init?.isRegex ?? false;
    }

    /**
     * Returns the search text as a regex-safe pattern:
     * a literal search escapes the text as is,
     * an explicit regex is used as is when valid or escaped when broken
     */
    public get effectiveSearchText(): string | null | undefined {
        if (this.cachedEffectiveSearchText === undefined || this.cachedEffectiveSearchText === null) {
            this.cachedEffectiveSearchText = NodeFilter.computeEffectiveSearchText(this.searchText, this.isRegex);
        }

        return this.cachedEffectiveSearchText;
    }

    private static computeEffectiveSearchText(searchText: string | null | undefined, isRegex: boolean): string | null | undefined {
        if (searchText === null || searchText === undefined || searchText === "") {
            return searchText;
        }

        if (!isRegex) {
            // the literal search - no metacharacter is interpreted
            return escapeRegExp(searchText);
        }

        try {
            // just validate the text is a correct regex pattern - nothing is matched here
            new RegExp(searchText);

            return searchText;
        } catch (error) {
            if (error instanceof SyntaxError) {
                // the pattern is broken, so search for it as a literal
                return escapeRegExp(searchText);
            }

            throw error;
        }
    }
}
