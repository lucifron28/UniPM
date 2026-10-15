# V2 expansion provisional development cases

This directory contains new synthetic cases for the additive PM analytics V2
contract. It is separate from the existing v1, v2, and v3 evaluation material.
The cases are provisional implementation fixtures: they are not a scored
benchmark, adviser-reviewed labels, a language-coverage claim, or held-out
evaluation data.

Each JSON Lines record has a stable local case ID, a synthetic question, and a
provisional expected interpretation. Valid cases specify the typed plan fields
relevant to the expected semantics; unsupported or clarification cases have no
executable plan. Do not use this file to rewrite historical scores or existing
evaluation splits. No provider calls or held-out evaluation are authorized by
this fixture.
