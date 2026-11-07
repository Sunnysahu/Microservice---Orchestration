-- MicroService - Orchestration


CREATE DATABASE SagaCommerce_OrderDb;
GO

USE SagaCommerce_OrderDb;
GO

CREATE TABLE dbo.Orders
(
    OrderId BIGINT IDENTITY(1,1) PRIMARY KEY,

    CustomerName NVARCHAR(100) NOT NULL,

    TotalAmount DECIMAL(18,2) NOT NULL,

    Status VARCHAR(30) NOT NULL,

    CreatedAt DATETIME2 NOT NULL
        DEFAULT SYSDATETIME()
);
GO

CREATE TABLE dbo.OrderItems
(
    OrderItemId BIGINT IDENTITY(1,1) PRIMARY KEY,

    OrderId BIGINT NOT NULL,

    ProductId INT NOT NULL,

    Quantity INT NOT NULL,

    UnitPrice DECIMAL(18,2) NOT NULL,

    CONSTRAINT FK_OrderItems_Orders
        FOREIGN KEY (OrderId)
        REFERENCES dbo.Orders(OrderId),

    CONSTRAINT CK_OrderItems_Quantity
        CHECK (Quantity > 0),

    CONSTRAINT CK_OrderItems_UnitPrice
        CHECK (UnitPrice >= 0)
);
GO

CREATE INDEX IX_OrderItems_OrderId
ON dbo.OrderItems(OrderId);
GO

ALTER TABLE dbo.Orders
ADD
    IdempotencyKey NVARCHAR(100) NULL,
    RequestHash CHAR(64) NULL;
GO

CREATE UNIQUE INDEX UX_Orders_IdempotencyKey
ON dbo.Orders(IdempotencyKey)
WHERE IdempotencyKey IS NOT NULL;
GO

SELECT * FROM OrderItems;
SELECT * FROM Orders;

---

CREATE DATABASE SagaCommerce_SagaDb;
GO

USE SagaCommerce_SagaDb;
GO

CREATE TABLE dbo.SagaInstances
(
    SagaId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_SagaInstances PRIMARY KEY,

    OrderId BIGINT NOT NULL,

    Status VARCHAR(30) NOT NULL,

    CurrentStep VARCHAR(50) NOT NULL,

    LastError NVARCHAR(1000) NULL,

    CreatedAt DATETIME2 NOT NULL
        DEFAULT SYSDATETIME(),

    UpdatedAt DATETIME2 NOT NULL
        DEFAULT SYSDATETIME(),

    RowVersion ROWVERSION NOT NULL
);
GO

CREATE UNIQUE INDEX UX_SagaInstances_OrderId
ON dbo.SagaInstances(OrderId);
GO


ALTER TABLE dbo.SagaInstances
ADD
    IdempotencyKey NVARCHAR(100) NULL,
    RequestHash CHAR(64) NULL,
    RetryCount INT NOT NULL
        CONSTRAINT DF_SagaInstances_RetryCount DEFAULT 0,
    NextAttemptAt DATETIME2 NULL,
    LeaseId UNIQUEIDENTIFIER NULL,
    LeaseUntil DATETIME2 NULL;
GO

CREATE UNIQUE INDEX UX_SagaInstances_IdempotencyKey
ON dbo.SagaInstances(IdempotencyKey)
WHERE IdempotencyKey IS NOT NULL;
GO

CREATE INDEX IX_SagaInstances_Recovery
ON dbo.SagaInstances(Status, NextAttemptAt, LeaseUntil)
INCLUDE (SagaId, OrderId, CurrentStep);
GO

SELECT * FROM SagaInstances;

---

CREATE DATABASE SagaCommerce_InventoryDb;
GO

USE SagaCommerce_InventoryDb;
GO

CREATE TABLE dbo.Products
(
    ProductId INT IDENTITY(1,1) PRIMARY KEY,

    ProductName NVARCHAR(100) NOT NULL,

    AvailableQuantity INT NOT NULL,

    CONSTRAINT CK_Products_Quantity
        CHECK (AvailableQuantity >= 0)
);
GO

CREATE TABLE dbo.InventoryReservations
(
    ReservationId UNIQUEIDENTIFIER NOT NULL
        PRIMARY KEY,

    OrderId BIGINT NOT NULL,

    ProductId INT NOT NULL,

    Quantity INT NOT NULL,

    Status VARCHAR(30) NOT NULL,

    CreatedAt DATETIME2 NOT NULL
        DEFAULT SYSDATETIME()
);
GO

INSERT INTO dbo.Products (ProductName, AvailableQuantity)
VALUES
('Laptop', 50),
('Keyboard', 100),
('Mouse', 150),
('Monitor', 30);
GO


ALTER TABLE dbo.InventoryReservations
ADD
    SagaId UNIQUEIDENTIFIER NULL,
    IdempotencyKey NVARCHAR(100) NULL;
GO


CREATE UNIQUE INDEX UX_InventoryReservations_Saga_Product
ON dbo.InventoryReservations(SagaId, ProductId)
WHERE SagaId IS NOT NULL;
GO

CREATE INDEX IX_InventoryReservations_Order_Status
ON dbo.InventoryReservations(OrderId, Status)
INCLUDE (SagaId, ProductId, Quantity);
GO

SELECT * FROM Products;
SELECT * FROM InventoryReservations;


---

CREATE DATABASE SagaCommerce_PaymentDb;
GO

USE SagaCommerce_PaymentDb;
GO

CREATE TABLE dbo.Payments
(
    PaymentId UNIQUEIDENTIFIER NOT NULL
        PRIMARY KEY,

    OrderId BIGINT NOT NULL,

    Amount DECIMAL(18,2) NOT NULL,

    Status VARCHAR(30) NOT NULL,

    IdempotencyKey NVARCHAR(100) NOT NULL,

    CreatedAt DATETIME2 NOT NULL
        DEFAULT SYSDATETIME(),

    CONSTRAINT UQ_Payments_IdempotencyKey
        UNIQUE (IdempotencyKey),

    CONSTRAINT CK_Payments_Amount
        CHECK (Amount > 0)
);
GO

ALTER TABLE dbo.Payments
ADD
    SagaId UNIQUEIDENTIFIER NULL,
    RequestHash CHAR(64) NULL;
GO

CREATE UNIQUE INDEX UX_Payments_OrderId
ON dbo.Payments(OrderId);
GO

CREATE INDEX IX_Payments_SagaId
ON dbo.Payments(SagaId)
WHERE SagaId IS NOT NULL;
GO

SELECT * FROM Payments;

---