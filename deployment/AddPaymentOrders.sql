IF OBJECT_ID(N'[dbo].[PaymentOrders]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PaymentOrders] (
        [PaymentOrderId] BIGINT IDENTITY(1,1) NOT NULL,
        [MerchantOrderId] NVARCHAR(80) NOT NULL,
        [UserId] INT NOT NULL,
        [PlanId] INT NOT NULL,
        [Provider] NVARCHAR(40) NOT NULL,
        [Status] NVARCHAR(40) NOT NULL,
        [AmountUsd] DECIMAL(18,2) NOT NULL,
        [AmountEgp] DECIMAL(18,2) NOT NULL,
        [AmountCents] INT NOT NULL,
        [Currency] NVARCHAR(10) NOT NULL,
        [ExchangeRateUsdToEgp] DECIMAL(18,4) NOT NULL CONSTRAINT [DF_PaymentOrders_Rate] DEFAULT (50),
        [PaymobIntentionId] NVARCHAR(100) NULL,
        [PaymobOrderId] NVARCHAR(100) NULL,
        [PaymobTransactionId] NVARCHAR(100) NULL,
        [CheckoutUrl] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NOT NULL,
        [PaidAt] DATETIME2 NULL,
        [FailureReason] NVARCHAR(500) NULL,
        CONSTRAINT [PK_PaymentOrders] PRIMARY KEY ([PaymentOrderId]),
        CONSTRAINT [FK_PaymentOrders_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]),
        CONSTRAINT [FK_PaymentOrders_Plans_PlanId]
            FOREIGN KEY ([PlanId]) REFERENCES [dbo].[Plans] ([PlanId])
    );

    CREATE UNIQUE INDEX [IX_PaymentOrders_MerchantOrderId] ON [dbo].[PaymentOrders] ([MerchantOrderId]);
    CREATE INDEX [IX_PaymentOrders_UserId] ON [dbo].[PaymentOrders] ([UserId]);
    CREATE INDEX [IX_PaymentOrders_Status] ON [dbo].[PaymentOrders] ([Status]);
    CREATE INDEX [IX_PaymentOrders_CreatedAt] ON [dbo].[PaymentOrders] ([CreatedAt]);
END
GO
