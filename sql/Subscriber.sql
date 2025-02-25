CREATE TABLE [dbo].[Subscriber] (
  [Did] VARCHAR(100) NOT NULL
, [Timestamp] DATETIME2(3) NOT NULL
, CONSTRAINT [PK_Subscriber] PRIMARY KEY ([Did])
);
GO


IF (EXISTS (SELECT * 
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = 'Subscriber'))
BEGIN
  ALTER TABLE [dbo].[Subscriber]
  ADD [Active] BIT CONSTRAINT DF_Subscriber_IsActive DEFAULT (1) NOT NULL
END
GO