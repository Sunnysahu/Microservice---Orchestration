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

---