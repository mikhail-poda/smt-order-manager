# Download Contract

This document describes the data an SMT line receives when an order is downloaded. It is written for developers of a receiving line who do not work with this code base. The reasons behind the contract design are described in the [architecture](architecture.md#integration-contract).

## Overview

| Property | Value |
|---|---|
| Format | JSON, UTF-8, one object per job |
| Current schema version | 1 |
| Approved example | [`tests/SmtOrderManager.Application.Tests/Contracts/order-download.v1.json`](../tests/SmtOrderManager.Application.Tests/Contracts/order-download.v1.json) |
| Producer | `OrderDownloadService` in the application layer |
| Reference consumer | The simulated SMT line (`SimulatedSmtLine`) |

The approved example is the authoritative reference. A contract test checks the generated format against it, so the code and the example cannot drift apart without a failing build. If this document and the example ever disagree, the example is right.

## Structure

A job contains the order, every board the order references, and the total component demand of the whole order.

### Root object

| Field | Type | Meaning |
|---|---|---|
| `schemaVersion` | integer | The schema version of this payload. Read it first, see [Versioning](#versioning). |
| `generatedAt` | date-time | The point in time at which the payload was created. |
| `order` | [Order](#order) | The order to produce. |
| `boards` | array of [Board](#board) | The boards referenced by the order, in the order of the order lines. Each board appears once. |
| `componentDemand` | array of [Component demand](#component-demand) | The total number of placements per component for the whole order. |

### Order

| Field | Type | Meaning |
|---|---|---|
| `id` | GUID | The order identifier. |
| `name` | string | The order name. Not unique. |
| `description` | string | The description. Empty when there is none, never `null`. |
| `orderDate` | date-time | The point in time at which the order was issued. |
| `lines` | array of [Order line](#order-line) | The boards to produce. At least one line, each board at most once. |

### Order line

| Field | Type | Meaning |
|---|---|---|
| `boardId` | GUID | The board to produce, matching the `id` of an entry in `boards`. |
| `quantity` | integer (32-bit) | The number of boards to produce. Always positive. |

### Board

| Field | Type | Meaning |
|---|---|---|
| `id` | GUID | The board identifier. |
| `name` | string | The board name. Not unique. |
| `description` | string | The description. Empty when there is none, never `null`. |
| `lengthMm` | decimal number | The board length in millimetres. Always positive. |
| `widthMm` | decimal number | The board width in millimetres. Always positive. |
| `billOfMaterials` | array of [BOM entry](#bom-entry) | The components on one board. At least one entry, each component at most once. |

### BOM entry

| Field | Type | Meaning |
|---|---|---|
| `componentId` | GUID | The component identifier. |
| `componentName` | string | The component name, included for readability on the line. |
| `quantity` | integer (32-bit) | The number of placements of this component on one board. Always positive. |

### Component demand

| Field | Type | Meaning |
|---|---|---|
| `componentId` | GUID | The component identifier. |
| `componentName` | string | The component name. |
| `totalQuantity` | integer (64-bit) | The number of placements of this component across all ordered boards. |

The total is calculated per component as the sum of *boards ordered × placements per board* over all order lines. Components appear in the order in which they first occur when the order lines and their bills of materials are read from top to bottom.

### Value formats

| Type | Format | Example |
|---|---|---|
| GUID | Lowercase, hyphenated, 36 characters | `0b000000-0000-0000-0000-000000000001` |
| date-time | ISO 8601 with the offset from UTC | `2026-10-05T08:30:00+02:00` |
| decimal number | JSON number with a dot as decimal separator, no unit | `50.8` |
| string | Any Unicode text, written unescaped | `Resistor 10 kΩ` |

Field names are camelCase. Field order carries no meaning.

## Example

This job orders 500 controller boards and 200 sensor boards. It is the same content as the approved example.

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-10-06T06:00:00+02:00",
  "order": {
    "id": "0a000000-0000-0000-0000-000000000001",
    "name": "Week 41",
    "description": "Controller and sensor boards",
    "orderDate": "2026-10-05T08:30:00+02:00",
    "lines": [
      { "boardId": "0b000000-0000-0000-0000-000000000001", "quantity": 500 },
      { "boardId": "0b000000-0000-0000-0000-000000000002", "quantity": 200 }
    ]
  },
  "boards": [
    {
      "id": "0b000000-0000-0000-0000-000000000001",
      "name": "Controller board",
      "description": "Main controller",
      "lengthMm": 160,
      "widthMm": 100,
      "billOfMaterials": [
        { "componentId": "0c000000-0000-0000-0000-000000000001", "componentName": "RES-10K-0402", "quantity": 12 },
        { "componentId": "0c000000-0000-0000-0000-000000000002", "componentName": "CAP-100N-0402", "quantity": 4 }
      ]
    },
    {
      "id": "0b000000-0000-0000-0000-000000000002",
      "name": "Sensor board",
      "description": "",
      "lengthMm": 50.8,
      "widthMm": 30.5,
      "billOfMaterials": [
        { "componentId": "0c000000-0000-0000-0000-000000000001", "componentName": "RES-10K-0402", "quantity": 3 },
        { "componentId": "0c000000-0000-0000-0000-000000000003", "componentName": "LED-RED-0603", "quantity": 2 }
      ]
    }
  ],
  "componentDemand": [
    { "componentId": "0c000000-0000-0000-0000-000000000001", "componentName": "RES-10K-0402", "totalQuantity": 6600 },
    { "componentId": "0c000000-0000-0000-0000-000000000002", "componentName": "CAP-100N-0402", "totalQuantity": 2000 },
    { "componentId": "0c000000-0000-0000-0000-000000000003", "componentName": "LED-RED-0603", "totalQuantity": 400 }
  ]
}
```

The resistor demand is 500 × 12 + 200 × 3 = 6,600. The capacitor is only on the controller board (500 × 4 = 2,000), the LED only on the sensor board (200 × 2 = 400).

## Versioning

Within a schema version, the payload only changes additively:

- New fields may be added at any level.
- Existing fields keep their name, type, unit and meaning.
- A field is never removed or made optional.

Any other change is breaking and introduces a new schema version with its own approved example. The producer writes exactly one version, `DownloadPayloadSchema.CurrentVersion`.

## Expectations of a consumer

A line that receives jobs is expected to:

1. **Check the schema version first.** Read only `schemaVersion`, and reject a job in a version it does not support, with a reason that names the version. A job in an unknown version must not be interpreted partially.
2. **Ignore unknown fields.** A job in a supported version may contain fields that were added after the consumer was built (tolerant reader). Read only the fields you need.
3. **Answer every job.** Accept it, or reject it with one reason per problem, for example one per board that exceeds the line's dimensions. A rejection is a normal answer; the order stays a draft and can be corrected and sent again.

The simulated line follows these rules in running code: it reads `schemaVersion` on its own first, then reads the job into its own types that declare only the order and board fields it uses, and ignores everything else.

## How the contract is tested

Both sides of the interface are tested against the same approved example:

| Side | Test | What it pins |
|---|---|---|
| Producer | `DownloadPayloadContractTests` | A fixed order serialized through the real mapping and serializer matches the approved example field by field. Whitespace and field order are ignored. |
| Consumer | `SimulatedSmtLineTests` | The simulated line accepts the approved example, accepts it with unknown fields added, and rejects it with a changed schema version or missing required fields. |

When the producer test fails, the change is either intended and additive (update the approved file) or breaking (introduce a new version). See [testing](testing.md#contract-tests) for details.