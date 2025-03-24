CREATE TABLE [dbo].[Subscriber] (
  [Did] VARCHAR(100) NOT NULL
, [Timestamp] DATETIME2(3) NOT NULL
, CONSTRAINT [PK_Subscriber] PRIMARY KEY ([Did])
);
GO


ALTER TABLE [dbo].[Subscriber]
  ADD [Active] BIT CONSTRAINT DF_Subscriber_IsActive DEFAULT (1) NOT NULL
GO

ALTER TABLE [dbo].[Subscriber]
  ADD [Handle] VARCHAR(100) NULL
GO

ALTER TABLE [dbo].[Subscriber]
  ADD [RKey] VARCHAR(100) NULL
GO

ALTER TABLE [dbo].[Subscriber]
  ALTER COLUMN [Handle] NVARCHAR(250) NULL
GO