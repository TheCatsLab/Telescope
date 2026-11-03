import { describe, expect, it } from "vitest";

import { FilterBy } from "../src/filtering/filterBy";
import { NodeFilter } from "../src/filtering/nodeFilter";
import { NodeFilterMatcher } from "../src/filtering/nodeFilterMatcher";

// port of src/tests/Cats.Telescope.Filtering.Tests/NodeFilterMatcherTests.cs

const resourceId = "invoice-processor-prod";

function makeFilter(searchText: string, filterBy: number, ignoreCase = true, isRegex = false): NodeFilter {
    return new NodeFilter({ searchText, filterByOptions: filterBy, ignoreCase, isRegex });
}

describe("guards", () => {
    it("null filter returns an empty result", () => {
        const result = NodeFilterMatcher.matchesCount(resourceId, "{}", null, null, null);

        expect(result.success).toBe(false);
    });

    it("empty search text returns an empty result", () => {
        const result = NodeFilterMatcher.matchesCount(resourceId, "{}", null, null, makeFilter("", FilterBy.ResourceName));

        expect(result.success).toBe(false);
    });

    it("null id returns an empty result", () => {
        const result = NodeFilterMatcher.matchesCount(null, "{}", null, null, makeFilter("invoice", FilterBy.ResourceName));

        expect(result.success).toBe(false);
    });

    it("all null sources do not throw", () => {
        const filter = makeFilter(
            "invoice",
            FilterBy.ResourceName
                | FilterBy.ResourceData
                | FilterBy.ResourceTagKeys
                | FilterBy.ResourceTagValues
                | FilterBy.ResourceEnvironmentVariableKeys
                | FilterBy.ResourceEnvironmentVariableValues,
        );

        const result = NodeFilterMatcher.matchesCount(resourceId, null, null, null, filter);

        // only the resource name has been searched - everything else is just skipped without exceptions
        expect(result.success).toBe(true);
        expect(result.matches).toHaveLength(1);
        expect(result.matches[0].by).toBe(FilterBy.ResourceName);
    });
});

describe("invalid regex patterns are treated as literal text", () => {
    it.each([
        "(", // unbalanced group
        "*", // quantifier without a preceding token
        "[a-", // unterminated character class
        "a{2,1}", // invalid quantifier range
    ])("%s does not throw and matches as literal", (searchText) => {
        const data = `prefix ${searchText} suffix`;
        const filter = makeFilter(searchText, FilterBy.ResourceData);

        const result = NodeFilterMatcher.matchesCount(resourceId, data, null, null, filter);

        expect(result.success).toBe(true);
        expect(result.matches.find((m) => m.by === FilterBy.ResourceData)!.count).toBe(1);
    });

    it("does not match when the literal is absent", () => {
        const filter = makeFilter("(", FilterBy.ResourceData | FilterBy.ResourceName);

        const result = NodeFilterMatcher.matchesCount(resourceId, "no such symbol here", null, null, filter);

        expect(result.success).toBe(false);
    });
});

describe("valid regex patterns keep working", () => {
    it("is applied when regex mode is explicit", () => {
        const filter = makeFilter("\\d{3}", FilterBy.ResourceData, true, true);

        const result = NodeFilterMatcher.matchesCount(resourceId, "abc123def456", null, null, filter);

        expect(result.success).toBe(true);
        expect(result.matches.find((m) => m.by === FilterBy.ResourceData)!.count).toBe(2);
    });

    it("alternation regex is still applied", () => {
        const filter = makeFilter("invoice|billing", FilterBy.ResourceName, true, true);

        const result = NodeFilterMatcher.matchesCount(resourceId, null, null, null, filter);

        expect(result.success).toBe(true);
        expect(result.matches.find((m) => m.by === FilterBy.ResourceName)!.count).toBe(1);
    });
});

describe("literal mode is the default", () => {
    it("treats regex metacharacters as literal text", () => {
        // the dot must match a literal dot, not any character
        const filter = makeFilter("a.c", FilterBy.ResourceName);

        const result = NodeFilterMatcher.matchesCount("abc", null, null, null, filter);

        expect(result.success).toBe(false);
    });

    it("literal metacharacters match themselves", () => {
        const filter = makeFilter("a.c", FilterBy.ResourceName);

        const result = NodeFilterMatcher.matchesCount("a.c", null, null, null, filter);

        expect(result.success).toBe(true);
    });
});

describe("null resource data does not break the filtering", () => {
    it("null data with the ResourceData flag does not throw", () => {
        const filter = makeFilter("anything", FilterBy.ResourceData);

        const result = NodeFilterMatcher.matchesCount(resourceId, null, null, null, filter);

        expect(result.success).toBe(false);
    });
});

describe("no false positives across dictionary entry boundaries", () => {
    it("a query spanning two tag keys does not match", () => {
        // "dev" + "prod" concatenated by mistake would contain "devprod"
        const tags = { dev: "1", prod: "2" };
        const filter = makeFilter("devprod", FilterBy.ResourceTagKeys);

        const result = NodeFilterMatcher.matchesCount(resourceId, null, tags, null, filter);

        expect(result.success).toBe(false);
    });

    it("a query spanning two tag values does not match", () => {
        const tags = { a: "dev", b: "prod" };
        const filter = makeFilter("devprod", FilterBy.ResourceTagValues);

        const result = NodeFilterMatcher.matchesCount(resourceId, null, tags, null, filter);

        expect(result.success).toBe(false);
    });

    it("a query spanning two env variable keys does not match", () => {
        const envVars = { env_dev: "1", env_prod: "2" };
        const filter = makeFilter("env_devenv_prod", FilterBy.ResourceEnvironmentVariableKeys);

        const result = NodeFilterMatcher.matchesCount(resourceId, null, null, envVars, filter);

        expect(result.success).toBe(false);
    });

    it("a query spanning two env variable values does not match", () => {
        const envVars = { a: "AAA", b: "BBB" };
        const filter = makeFilter("AAABBB", FilterBy.ResourceEnvironmentVariableValues);

        const result = NodeFilterMatcher.matchesCount(resourceId, null, null, envVars, filter);

        expect(result.success).toBe(false);
    });

    it("a common prefix matches all env variable keys", () => {
        const envVars = { env_dev: "1", env_prod: "2" };
        const filter = makeFilter("env_", FilterBy.ResourceEnvironmentVariableKeys);

        const result = NodeFilterMatcher.matchesCount(resourceId, null, null, envVars, filter);

        expect(result.success).toBe(true);
        expect(result.matches.find((m) => m.by === FilterBy.ResourceEnvironmentVariableKeys)!.count).toBe(2);
    });
});

describe("case sensitivity", () => {
    it("ignore-case comparison matches a different case", () => {
        const filter = makeFilter("INVOICE", FilterBy.ResourceName, true);

        const result = NodeFilterMatcher.matchesCount(resourceId, null, null, null, filter);

        expect(result.success).toBe(true);
    });

    it("case-sensitive comparison does not match a different case", () => {
        const filter = makeFilter("INVOICE", FilterBy.ResourceName, false);

        const result = NodeFilterMatcher.matchesCount(resourceId, null, null, null, filter);

        expect(result.success).toBe(false);
    });
});

describe("filter flags independence", () => {
    it("only selected flags produce matches", () => {
        const tags = { team: "cats" };
        const filter = makeFilter("team", FilterBy.ResourceTagKeys);

        // the data contains the text as well, but the flag is not set
        const result = NodeFilterMatcher.matchesCount(resourceId, "team is mentioned here", tags, null, filter);

        expect(result.success).toBe(true);
        expect(result.matches).toHaveLength(1);
        expect(result.matches[0].by).toBe(FilterBy.ResourceTagKeys);
    });
});
