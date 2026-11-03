import { FilterBy, FilterByOptions, hasFlag } from "./filterBy";
import { NodeFilter } from "./nodeFilter";
import { NodeFilterResult } from "./nodeFilterResult";

/**
 * Calculates how many times a NodeFilter matches a resource data snapshot.
 * Kept free of any UI or VS Code dependencies so the logic can be unit tested.
 * Port of src/Core/Models/NodeFilterMatcher.cs.
 */
export const NodeFilterMatcher = {
    /**
     * Applies {@link filter} to the resource data and calculates how many matches there are
     *
     * @param id resource name
     * @param data resource data (json) - can be null
     * @param tags resource tags - can be null
     * @param environmentVariables resource environment variables (app settings) - can be null
     * @param filter node filter to apply
     */
    matchesCount(
        id: string | null | undefined,
        data: string | null | undefined,
        tags: Readonly<Record<string, string>> | null | undefined,
        environmentVariables: Readonly<Record<string, string>> | null | undefined,
        filter: NodeFilter | null | undefined,
    ): NodeFilterResult {
        const result = new NodeFilterResult();

        if (!filter) {
            return result;
        }

        if (filter.searchText === null || filter.searchText === undefined || filter.searchText === "") {
            return result;
        }

        if (id === null || id === undefined) {
            return result;
        }

        const flags = filter.ignoreCase ? "gi" : "g";
        const pattern = filter.effectiveSearchText ?? "";

        const countMatches = (text: string): number => {
            const matches = text.match(new RegExp(pattern, flags));
            return matches ? matches.length : 0;
        };

        // filter by name
        if (hasFlag(filter.filterByOptions, FilterBy.ResourceName)) {
            const findings = countMatches(id);

            if (findings > 0) {
                result.matches.push({ by: FilterBy.ResourceName, count: findings });
            }
        }

        // filter by data
        if (hasFlag(filter.filterByOptions, FilterBy.ResourceData)) {
            const findings = countMatches(data ?? "");

            if (findings > 0) {
                result.matches.push({ by: FilterBy.ResourceData, count: findings });
            }
        }

        // filter by tag keys
        if (hasFlag(filter.filterByOptions, FilterBy.ResourceTagKeys) && tags) {
            // the separator prevents a match spanning over two adjacent keys
            const findings = countMatches(Object.keys(tags).join("\n"));

            if (findings > 0) {
                result.matches.push({ by: FilterBy.ResourceTagKeys, count: findings });
            }
        }

        // filter by tag values
        if (hasFlag(filter.filterByOptions, FilterBy.ResourceTagValues) && tags) {
            const findings = countMatches(Object.values(tags).join("\n"));

            if (findings > 0) {
                result.matches.push({ by: FilterBy.ResourceTagValues, count: findings });
            }
        }

        // filter by environment variable keys
        if (hasFlag(filter.filterByOptions, FilterBy.ResourceEnvironmentVariableKeys) && environmentVariables) {
            const findings = countMatches(Object.keys(environmentVariables).join("\n"));

            if (findings > 0) {
                result.matches.push({ by: FilterBy.ResourceEnvironmentVariableKeys, count: findings });
            }
        }

        // filter by environment variable values
        if (hasFlag(filter.filterByOptions, FilterBy.ResourceEnvironmentVariableValues) && environmentVariables) {
            const findings = countMatches(Object.values(environmentVariables).join("\n"));

            if (findings > 0) {
                result.matches.push({ by: FilterBy.ResourceEnvironmentVariableValues, count: findings });
            }
        }

        return result;
    },
};
