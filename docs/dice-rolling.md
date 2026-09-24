# Shared dice rolling

Rules Core owns the canonical automatic dice-selection semantics used by Dorks & Dice tools.

## API

```text
GET  /api/rules/dice
POST /api/rules/dice/roll
```

The roll endpoint accepts:

```json
{
  "sides": 20,
  "selectionMode": "normal",
  "modifier": 0,
  "repeat": 1
}
```

`repeat` produces independent outcomes in one request so tools can roll groups without creating one HTTP request per die.

## Selection modes

- `normal`: roll once.
- `advantage`: roll twice and use the higher number.
- `disadvantage`: roll twice and use the lower number.
- `emphasis`: roll twice and use the number furthest from 10.

Emphasis is defined only for a d20. If two different results are equally far from 10, Rules Core returns both candidate indices with `requiresChoice=true` and no selected result. There is no invented tie-break rule.

Automatic rolls use the platform cryptographic random-number generator. Consumers must not duplicate these selection rules in individual tools. A tool that generates a consequential die result should call this service and preserve the returned raw rolls when it needs an audit display.

The same vocabulary is used by Character mechanics conditional roll rules through `CharacterMechanicRollModes`.
