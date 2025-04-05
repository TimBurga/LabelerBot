CREATE TABLE [dbo].[ImagePost] (
  [Did] VARCHAR(100) NOT NULL
, [Cid] VARCHAR(100) NOT NULL
, [ValidAlt] BIT NOT NULL
, [Timestamp] DATETIME2(3) NOT NULL
, CONSTRAINT [PK_ImagePost] PRIMARY KEY ([Did], [Cid])
, CONSTRAINT [FK_ImagePost_Subscriber] FOREIGN KEY ([Did]) REFERENCES [dbo].[Subscriber]([Did])
);
GO

ALTER TABLE [dbo].[ImagePost]
  ADD [RKey] VARCHAR(100) NULL
GO