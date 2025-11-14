# Banking Service - .NET 9 Implementation

A comprehensive banking service implementation following SOLID principles and Clean Architecture patterns.

## Features

### Core Operations
- **Account Creation**: Create accounts with initial deposits
- **Deposits**: Add funds to accounts with idempotency protection
- **Withdrawals**: Remove funds with overdraft protection and idempotency
- **Transfers**: Move funds between accounts atomically with idempotency
- **Balance Inquiry**: Check current account balance
- **Transaction History**: View all transactions with status tracking

### Business Rules Enforced
- No overdrafts allowed
- All amounts must be positive
- Transfers are atomic (both succeed or both fail)
- Account numbers are auto-generated and unique
- Inactive accounts cannot perform operations
- All write operations require unique idempotency keys
- Transaction lifecycle tracking (Pending → Posted/Failed/Reversed)
- Transfers require both source and destination accounts to be active
- Transfers cannot be made to the same account
- Multi-currency and time culture support

### Concurrency & Thread Safety
- **Account-level pessimistic locking** prevents race conditions during concurrent operations
- **Fine-grained locking** allows parallel operations on different accounts
- **Deadlock prevention** through sorted lock acquisition and duplicate detection
- **Idempotency enforcement** prevents duplicate operations from network retries
- **Thread-safe repositories** using concurrent data structures

### Transaction Management
- **Transaction Status Tracking**: Pending, Posted, Failed, Reversed
- **Idempotent Operations**: Duplicate requests return cached results
- **Audit Trail**: Complete history with timestamps and status transitions
- **Operation Recovery**: Failed operations can be retried with same idempotency key

### Design Patterns Used
- **Repository Pattern**: Abstracts data access
- **Unit of Work Pattern**: Manages database transactions
- **Result Pattern**: Handles success/failure without exceptions for expected failures
- **Dependency Injection**: Manages object lifetime and dependencies
- **DTO Pattern**: Separates domain models from API contracts
- **Pessimistic Locking Pattern**: Ensures data consistency in concurrent scenarios
- **Idempotency Pattern**: Prevents duplicate processing of financial operations
- **State Pattern**: Transaction status lifecycle management
- **Domain Events** (planned): Architecture supports future event-driven extensions.

## Technology Stack
- .NET 9
- C# 13
- xUnit (for unit testing)
- Microsoft.Extensions.DependencyInjection
- In-Memory repositories for testing and demo

## Running the Application

### Prerequisites
- .NET 9 SDK installed

### Build and Run
```bash
# Build solution
dotnet build

# Run tests
dotnet test

# Run console demo
dotnet run --project BankingService.ConsoleApp
```

## Testing

Comprehensive unit tests cover:
- Account creation with valid and invalid data
- Deposits with various scenarios
- Withdrawals including overdraft prevention
- Transfers between accounts
- Balance inquiries
- Transaction history
- Edge cases and error handling

## Extension Points

The architecture is designed for easy extension:

1. **Database Integration**: Replace in-memory repositories with Entity Framework Core
2. **Event Sourcing**: Add domain events to track all state changes
3. **Validation**: Add FluentValidation for complex validation rules
4. **Logging**: Integrate Serilog or similar logging framework
5. **Caching**: Add distributed caching layer
6. **Metrics**: Integrate with Application Insights
7. **API Layer**: Add REST API using ASP.NET Core
8. **Authorization**: Implement role-based access control

## Real-World Considerations Implemented

1. **Concurrency**: Thread-safe repositories using ConcurrentDictionary
2. **Transaction Integrity**: UnitOfWork ensures atomic operations
3. **Audit Trail**: All transactions are logged with timestamps
4. **Error Handling**: Graceful error handling with descriptive messages
5. **Business Rules**: Overdraft prevention, validation, etc.
6. **Immutability**: Domain entities use private setters
7. **Type Safety**: Strong typing with records for DTOs

## Future Enhancements

- Interest calculation
- Account types (Checking, Savings, etc.)
- Transaction fees
- Scheduled transfers
- Account statements
- Notifications
- Fraud detection
