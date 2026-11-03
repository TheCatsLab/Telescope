import { describe, expect, it } from "vitest";

import { NodeFilter } from "../src/filtering/nodeFilter";

// port of src/tests/Cats.Telescope.Filtering.Tests/NodeFilterTests.cs

function escapeRegExp(text: string): string {
    return text.replace(/[-.*+?^${()|[\]\\]/g, "\\$&");
}

describe("NodeFilter.effectiveSearchText", () => {
    describe("regex mode - valid pattern is returned as is", () => {
        it.each([
            "invoice", // plain text
            "\\d{3}", // valid regex
            "(a|b)+", // valid alternation
            "[a-z]+", // valid character class
        ])("%s", (searchText) => {
            const filter = new NodeFilter({ searchText, isRegex: true });

            expect(filter.effectiveSearchText).toBe(searchText);
        });
    });

    describe("default mode - is escaped to literal", () => {
        it.each([
            ["invoice", "invoice"],
            ["\\d{3}", "\\\\d\\{3}"],
            ["(a|b)+", "\\(a\\|b\\)\\+"],
        ])("%s -> %s", (searchText, expected) => {
            const filter = new NodeFilter({ searchText });

            expect(filter.effectiveSearchText).toBe(expected);
        });
    });

    describe("regex mode - invalid pattern is escaped to literal", () => {
        it.each([
            "(", // unbalanced group
            "*", // quantifier without a preceding token
            "a{2,1}", // invalid quantifier range
        ])("%s", (searchText) => {
            const filter = new NodeFilter({ searchText });

            expect(filter.effectiveSearchText).toBe(escapeRegExp(searchText));
        });
    });

    it("escaped literal finds plain text", () => {
        const filter = new NodeFilter({ searchText: "(", isRegex: true });

        // the escaped pattern must behave as a literal search
        expect("a ( b".match(new RegExp(filter.effectiveSearchText!, "g"))!.length).toBe(1);
    });

    it("null or empty search text is returned as is", () => {
        const nullFilter = new NodeFilter({ searchText: null });
        const emptyFilter = new NodeFilter({ searchText: "" });

        expect(nullFilter.effectiveSearchText).toBeNull();
        expect(emptyFilter.effectiveSearchText).toBe("");
    });

    it("is cached", () => {
        const filter = new NodeFilter({ searchText: "(", isRegex: true });

        const first = filter.effectiveSearchText;
        filter.searchText = "changed";

        // the value is computed once per filter instance
        expect(filter.effectiveSearchText).toBe(first);
    });
});
