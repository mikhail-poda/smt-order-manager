# Testing

This document describes how the application is tested: the strategy per layer, what the tests cover and where to find representative examples. It does not list every test. The test names describe their cases (`Method_Scenario_Expectation`), and the test explorer of an IDE or `dotnet test` shows the complete, current list.

## Strategy

Each production project except the CLI has its own test project, and each test project references only the layers it tests. Tests therefore follow the same dependency rule as the code, see [project dependencies](architecture.md#project-dependencies).

| Test project | Tests against | Test doubles |
|---|---|---|
| `SmtOrderManager.Domain.Tests` | Aggregates, value objects and the domain service, in isolation | None needed |
| `SmtOrderManager.Application.Tests` | Use cases, payload mapping and serialization, the contract | In-memory repositories, a fake SMT line, a fixed time provider |
| `SmtOrderManager.Infrastructure.Tests` | JSON files, the simulated SMT line, service registrations | Real files in temporary directories, a fixed time provider |

The domain is where the business rules live, so it has the most tests. The application tests check that use cases combine the rules correctly, for example that a batch is saved completely or not at all. The infrastructure tests check the technical promises: data survives a restart, writes are atomic, the simulated line answers as specified.

The console user interface has no automated tests. It only reads input, calls the use cases and prints their results, so its logic is covered by the tests of the layers below. Its wiring is covered by the service registration tests.

## Coverage by topic

Each topic names representative tests. Their neighbours in the same class cover the variations.

### Domain rules

| Topic | Representative tests |
|---|---|
| Identity and equality of aggregate roots | `AggregateRootTests.Equals_WithDifferentTypeAndSameId_ReturnsFalse` |
| Value objects validate on creation and compare by value | `BomEntryTests.Constructor_WithNonPositiveQuantity_ThrowsDomainException`, `BomEntryTests.Equals_WithSameValues_ReturnsTrue` |
| Cardinality: at least one line or BOM entry | `OrderTests.RemoveLine_WithLastLine_ThrowsAndKeepsLine`, `OrderTests.Create_WithoutLines_ThrowsDomainException` |
| Each board or component at most once | `OrderTests.Create_WithDuplicateBoard_ThrowsDomainException`, `OrderTests.SetLine_WithExistingBoard_ReplacesLineInPlace` |
| A failed change leaves the aggregate unchanged | `OrderTests.Rename_WithBlankName_ThrowsAndKeepsOriginalName`, `OrderTests.SetLine_WithNonPositiveQuantity_ThrowsAndKeepsLines` |
| Encapsulation of the lists | `OrderTests.Lines_IsNotExposedAsMutableList`, `OrderTests.Create_WhenSourceListChangesAfterwards_KeepsOriginalLines` |
| Restoring persisted aggregates applies the same rules | `OrderTests.Restore_WithDownloadedStatusWithoutDownloadTime_ThrowsDomainException` |
| Timestamps keep their offset | `OrderTests.Create_WithOffset_KeepsOffset` |

### Order status

| Topic | Representative tests |
|---|---|
| New orders are drafts | `OrderTests.Create_SetsStatusToDraft` |
| A downloaded order rejects every edit | `OrderTests.EditingMethod_OnDownloadedOrder_ThrowsDomainException` |
| Repeated downloads record the latest time | `OrderTests.MarkDownloaded_OnDownloadedOrder_RecordsMostRecentDownloadTime` |
| A downloaded order cannot be updated or removed through the use cases | `OrderServiceTests.UpdateAsync_WithDownloadedOrder_SavesNothingAndReportsIt`, `OrderServiceTests.RemoveAsync_WithDownloadedOrder_RemovesNothingAndReportsIt` |

### Total component demand

| Topic | Representative tests |
|---|---|
| The example from the domain model (500 × 12 + 200 × 3 = 6,600) | `ComponentDemandCalculatorTests.Calculate_WithExampleFromDomainModel_SumsResistorDemandAcrossBoards` |
| Different components stay separate | `ComponentDemandCalculatorTests.Calculate_WithDifferentComponents_KeepsThemSeparateInOrderOfFirstAppearance` |
| No overflow with maximum quantities | `ComponentDemandCalculatorTests.Calculate_WithMaximumQuantities_DoesNotOverflow` |

### Batch operations and violations

| Topic | Representative tests |
|---|---|
| A valid batch is saved completely | `ComponentServiceTests.CreateAsync_WithValidBatch_SavesAllAndReturnsDetails` |
| One invalid item rejects the whole batch, and every problem is reported | `ComponentServiceTests.CreateAsync_WithInvalidItems_SavesNothingAndReportsEachItem`, `BoardServiceTests.CreateAsync_WithSeveralInvalidBoards_ReportsEveryBoard` |
| References to other aggregates must exist | `BoardServiceTests.CreateAsync_WithMissingComponent_SavesNothingAndReportsIt`, `OrderServiceTests.CreateAsync_WithMissingBoard_SavesNothingAndReportsIt` |
| The same item twice in one batch | `ComponentServiceTests.UpdateAsync_WithSameComponentTwice_SavesNothingAndReportsDuplicate` |
| An empty batch is a programming error | `ComponentServiceTests.CreateAsync_WithEmptyBatch_ThrowsArgumentException` |

### Restricted deletion

| Topic | Representative tests |
|---|---|
| A component used by boards is not removed, and every board is named | `ComponentServiceTests.RemoveAsync_WithComponentUsedByBoards_RemovesNothingAndReportsEveryBoard` |
| A board used by orders is not removed, and every order is named | `BoardServiceTests.RemoveAsync_WithBoardUsedByOrders_RemovesNothingAndReportsEveryOrder` |

### Order download

| Topic | Representative tests |
|---|---|
| Acceptance marks the order downloaded | `OrderDownloadServiceTests.DownloadAsync_WhenLineAccepts_MarksOrderDownloadedAndReturnsAnswer` |
| Rejection and line failure leave the order a draft | `OrderDownloadServiceTests.DownloadAsync_WhenLineRejects_KeepsOrderDraftAndReturnsReasons`, `OrderDownloadServiceTests.DownloadAsync_WhenLineUnavailable_ThrowsAndKeepsOrderDraft` |
| Missing data is reported and nothing is sent | `OrderDownloadServiceTests.DownloadAsync_WithMissingBoard_ReturnsViolationAndSendsNothing` |
| The line receives JSON, not objects | `OrderDownloadServiceTests.DownloadAsync_WithOrder_SendsSerializedPayload` |
| Mapping to the payload | `OrderDownloadPayloadMapperTests.Map_WithAdditionalBoards_IncludesReferencedBoardsInLineOrder` |
| Serializer settings | `DownloadPayloadSerializerTests.Serialize_WithPayload_UsesLineFeedsOnEveryPlatform` |

### Contract tests

The download payload is an interface to separately developed applications, so its format is pinned by an approved example: `Contracts/order-download.v1.json` in the application tests. Both sides of the interface are tested against that one file.

| Side | Test | What it checks |
|---|---|---|
| Producer | `DownloadPayloadContractTests.Serialize_WithFixture_MatchesApprovedVersion1Example` | A fixed order, run through the real demand calculation, mapping and serialization, matches the approved example. The comparison uses `JsonNode.DeepEquals`, so whitespace and field order do not matter, but any change to a name, type or value fails. |
| Consumer | `SimulatedSmtLineTests.DownloadAsync_WithApprovedVersion1Example_Accepts` | The simulated line accepts the approved example. The infrastructure test project links the file from the application tests instead of copying it. |
| Consumer | `SimulatedSmtLineTests.DownloadAsync_WithUnknownFields_AcceptsTolerantly` | Unknown fields at any level are ignored. |
| Consumer | `SimulatedSmtLineTests.DownloadAsync_WithUnsupportedVersionAndOtherwiseUnreadableJob_RejectsForVersionOnly` | The version is checked before anything else is read. |

When the producer test fails, the change is either intended and additive, so the approved file is updated, or breaking, so a new schema version with its own approved file is introduced. The consumer tests then show whether the line still accepts the format.

### Simulated SMT line

| Topic | Representative tests |
|---|---|
| Unsupported versions are rejected with the supported ones named | `SimulatedSmtLineTests.DownloadAsync_WithUnsupportedSchemaVersion_RejectsNamingVersions` |
| Unreadable or incomplete jobs are rejected, not thrown | `SimulatedSmtLineTests.DownloadAsync_WithSupportedVersionButMissingField_Rejects` |
| Board dimensions: within, at and above the limits | `SimulatedSmtLineTests.DownloadAsync_WithBoardExactlyAtLimits_Accepts`, `SimulatedSmtLineTests.DownloadAsync_WithBoardAboveLimit_RejectsNamingBoardAndSizes` |
| Every oversized board is listed | `SimulatedSmtLineTests.DownloadAsync_WithSeveralBoardsAboveLimit_ListsEveryBoard` |
| Boards are not rotated | `SimulatedSmtLineTests.DownloadAsync_WithBoardThatWouldFitOnlyRotated_Rejects` |
| Accepted jobs land in the inbox unchanged and never overwrite each other | `SimulatedSmtLineTests.DownloadAsync_WhenAccepted_WritesJobUnchangedToInboxFileNamedAfterOrderAndTime`, `SimulatedSmtLineTests.DownloadAsync_SameOrderTwiceAtSameTime_KeepsBothJobs` |
| Technical failures throw instead of rejecting | `SimulatedSmtLineTests.DownloadAsync_WhenInboxCannotBeCreated_ThrowsSmtLineUnavailableException`, `SimulatedSmtLineTests.DownloadAsync_WhenUnavailable_ThrowsEvenForUnreadableJob` |

### Persistence

| Topic | Representative tests |
|---|---|
| Every field survives a restart | `JsonOrderRepositoryTests.SaveAsync_ThenGetByIdAsyncAfterRestart_RestoresDownloadedStatus`, `JsonBoardRepositoryTests.SaveAsync_ThenGetByIdAsyncAfterRestart_RestoresEveryField` |
| Atomic writes: a failed update leaves the file unchanged | `JsonFileStoreTests.UpdateAsync_WhenChangeThrows_KeepsDataFileUnchanged`, `JsonFileStoreTests.UpdateAsync_WithLeftoverTemporaryFileFromCrash_ReplacesIt` |
| Concurrent updates lose nothing | `JsonFileStoreTests.UpdateAsync_WithConcurrentCalls_LosesNoUpdate` |
| Damaged files are reported, never treated as empty | `JsonFileStoreTests.LoadAsync_WithInvalidContent_ThrowsInvalidDataExceptionNamingFile`, `JsonComponentRepositoryTests.GetByIdAsync_WithStoredDataBreakingDomainRule_ThrowsInvalidDataExceptionNamingAggregate` |
| Repository contract: replace in place, batch writes, search | `JsonComponentRepositoryTests.SaveAsync_WithNewAndExistingAggregates_ReplacesAndAppendsInOneBatch` |

The application tests rely on in-memory fakes that must behave like the real repositories. `InMemoryRepositoryTests` checks that, for example that a loaded aggregate is a copy, so a use case that forgets to save is caught.

### Architecture and configuration

| Topic | Representative tests |
|---|---|
| Infrastructure does not depend on the CLI | `DependencyRuleTests.InfrastructureAssembly_DoesNotReferenceCli` |
| Every use case can be resolved | `ServiceRegistrationTests.AddApplicationAndInfrastructure_ResolvesEveryUseCase` |
| Invalid configuration fails at startup | `ServiceRegistrationTests.ValidateOnStart_WithInvalidValue_ThrowsNamingOption` |

The other dependency rules (domain references nothing, application references only the domain) are enforced by the project references themselves, so the compiler checks them.

## Conventions

- **Names** follow `Method_Scenario_Expectation`, for example `RemoveLine_WithLastLine_ThrowsAndKeepsLine`. A test checks one behaviour, and its name says which.
- **Structure** follows arrange, act, assert, separated by blank lines.
- **Deterministic values.** Time comes from a `FixedTimeProvider`, never from the system clock. The contract fixture uses fixed, readable identifiers (`0a…` for the order, `0b…` for boards, `0c…` for components).
- **Isolation.** File system tests work in their own temporary directory under the system temp folder and delete it afterwards, so tests can run in parallel and leave nothing behind.
- **Cancellation.** Tests pass `TestContext.Current.CancellationToken`, so a cancelled test run stops promptly.
- **Business rules are tested where they live.** A rule of an aggregate is tested in the domain tests; the application tests only check that a broken rule becomes a violation and nothing is saved.

## Running the tests

From the repository root:

```bash
dotnet test
```

Each test project can also be run on its own by passing its folder, for example `tests/SmtOrderManager.Domain.Tests`. The test explorers of Visual Studio, Rider and VS Code show every test by name and allow running a single class or test.