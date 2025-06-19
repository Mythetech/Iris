# Command Query Responsibility Segregation (CQRS)

## Understanding Command Query Separation (CQS)

**CQS Principle**: The [Command Query Separation (CQS)](https://martinfowler.com/bliki/CommandQuerySeparation.html) principle states that every method should either be a **command** (which changes the system’s state) or a **query** (which retrieves information without side effects). This separation helps create a clear boundary between operations that modify the state of an application and those that retrieve data, leading to more predictable, manageable code.

In CQS:
- **Queries**: These methods are *free of side effects* and return data without altering the system’s state. They are “safe” operations that can be used freely across different parts of an application.
- **Commands**: These methods perform actions that change the system’s state. Commands are intentional actions—changes that are isolated and controlled within their bounded contexts, especially relevant in **event-driven architectures**.

## Applying CQS in Practice

Separating commands and queries not only improves readability and predictability, but it also aligns well with principles of **information hiding** and **encapsulation**. For example, we can enforce this separation by using distinct interfaces:

- **`IService`**: Handles query operations, fetching data without side effects.
- **`IWriteService`**: Handles commands, changing the state when needed.  
   
By keeping **write services private to specific modules** and **encouraging inter-module communication via events**, we further encapsulate state changes, ensuring they’re only triggered within controlled boundaries. This is especially valuable in **event-driven** systems, where events signal changes to other parts of the system without exposing direct state modification methods.

## Extending CQS to CQRS

**CQRS** builds on CQS principles by **splitting commands and queries into entirely distinct models** (and, in some cases, separate services or even databases) when their requirements diverge significantly. In CQRS, each operation can evolve and scale independently, optimized specifically for **reading** or **writing** to improve performance and clarity.

For instance:
- **Query Model**: Optimized for reading and retrieving data, often designed to support high-performance queries, like using denormalized views.
- **Command Model**: Focused on handling commands that modify data, often built with more complex logic to ensure data consistency and integrity.

With CQRS, it's often possible to scale read and write operations independently or leverage **event sourcing** to log changes that eventually update the query model asynchronously.

For further details on CQRS, refer to [CQRS on Microsoft Docs](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs).