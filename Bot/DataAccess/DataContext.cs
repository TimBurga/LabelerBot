﻿using FishyFlip.Models;
using LabelerBot.Bot.Models;
using Microsoft.EntityFrameworkCore;

namespace LabelerBot.Bot.DataAccess;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{
    public DbSet<ImagePost> Posts { get; set; }
    public DbSet<Subscriber> Subscribers { get; set; }
    public DbSet<Label> Labels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ImagePost>(entity =>
        {
            entity.ToTable("ImagePost");

            entity.HasKey(x => new { x.Did, x.Cid });

            entity.Property(x => x.Did)
                .HasMaxLength(100)
                .HasConversion(
                    save => save.ToString(),
                    load => ATDid.Create(load)!);

            entity.Property(x => x.Cid)
                .HasMaxLength(100);
        });

        modelBuilder.Entity<Subscriber>(entity =>
        {
            entity.ToTable("Subscriber");

            entity.HasKey(x => x.Did);

            entity.Property(x => x.Did)
                .HasMaxLength(100)
                .HasConversion(
                    save => save.ToString(),
                    load => ATDid.Create(load)!);

            entity.Property(x => x.Active).HasDefaultValue(true);

            entity.Property(x => x.Handle).HasMaxLength(100);
            
            entity.Property(x => x.Rkey)
                .HasColumnName("RKey")
                .HasMaxLength(100);
        });

        modelBuilder.Entity<Label>(entity =>
        {
            entity.ToTable("Label");

            entity.HasKey(x => new {x.Did, x.Level});

            entity.Property(x => x.Did)
                .HasMaxLength(100)
                .HasConversion(
                    save => save.ToString(),
                    load => ATDid.Create(load)!);

            entity.Property(x => x.Level)
                .HasMaxLength(20)
                .HasConversion(
                    save => save.ToString(),
                    load => (LabelLevel)Enum.Parse(typeof(LabelLevel), load));
        });
    }
}

