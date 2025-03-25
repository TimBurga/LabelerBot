CREATE TABLE [dbo].[Label] (
  [Did] VARCHAR(100) NOT NULL
, [Level] VARCHAR(20) NOT NULL
, [Timestamp] DATETIME2(3) NOT NULL
, CONSTRAINT [PK_Label] PRIMARY KEY ([Did])
, CONSTRAINT [FK_Label_Subscriber] FOREIGN KEY ([Did]) REFERENCES [dbo].[Subscriber]([Did])
);