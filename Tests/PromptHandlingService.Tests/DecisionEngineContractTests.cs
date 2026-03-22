using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Communication;
using DecisionEngineService;
using Game;

namespace PromptHandlingService.Tests;

/// <summary>
/// DES-MISS-005: Decision service contract regression tests.
/// Covers MLModel prediction/learning, GetNextActions routing, and FileSystemWatcher lifecycle.
/// </summary>
public class DecisionEngineContractTests
{
    // ===== MLModel contract =====

    [Fact]
    public void MLModel_Predict_ReturnsEmptyList_ForEmptySnapshot()
    {
        var actions = MLModel.Predict(new WoWActivitySnapshot());

        Assert.NotNull(actions);
        Assert.Empty(actions);
    }

    [Fact]
    public void MLModel_Predict_ReturnsCastSpell_WhenPlayerHealthBelowHalf()
    {
        var snapshot = new WoWActivitySnapshot
        {
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 40, MaxHealth = 100 }
            }
        };

        var actions = MLModel.Predict(snapshot);

        Assert.NotEmpty(actions);
        Assert.Contains(actions, a => a.Actions.Any(m => m.ActionType == ActionType.CastSpell));
    }

    [Fact]
    public void MLModel_Predict_NeverThrows_ForNullSubfields()
    {
        // Player with no Unit, no Position — should not throw
        var snapshot = new WoWActivitySnapshot { Player = new WoWPlayer() };
        var ex = Record.Exception(() => MLModel.Predict(snapshot));
        Assert.Null(ex);
    }

    [Fact]
    public void MLModel_Predict_IsDeterministic_SameInputSameOutput()
    {
        var snapshot = new WoWActivitySnapshot
        {
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 30, MaxHealth = 100 }
            }
        };

        var first = MLModel.Predict(snapshot);
        var second = MLModel.Predict(snapshot);

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Actions.Count, second[i].Actions.Count);
            for (int j = 0; j < first[i].Actions.Count; j++)
            {
                Assert.Equal(first[i].Actions[j].ActionType, second[i].Actions[j].ActionType);
            }
        }
    }

    [Fact]
    public void MLModel_LearnFromSnapshot_NullSafe()
    {
        var model = new MLModel([]);
        var ex = Record.Exception(() => model.LearnFromSnapshot(null!));
        Assert.Null(ex);
    }

    [Fact]
    public void MLModel_LearnFromSnapshot_IncrementsWeight_OnSuccess()
    {
        var model = new MLModel([0f, 0f, 0f]);

        var snapshot = new WoWActivitySnapshot
        {
            CurrentAction = new ActionMessage
            {
                ActionType = ActionType.Goto, // index 1
                ActionResult = ResponseResult.Success
            }
        };

        model.LearnFromSnapshot(snapshot);

        var weights = model.GetWeights();
        Assert.Equal(1f, weights[(int)ActionType.Goto]);
    }

    [Fact]
    public void MLModel_LearnFromSnapshot_DecrementsWeight_OnFailure()
    {
        var model = new MLModel([0f, 0f, 0f]);

        var snapshot = new WoWActivitySnapshot
        {
            CurrentAction = new ActionMessage
            {
                ActionType = ActionType.Goto, // index 1
                ActionResult = ResponseResult.Failure
            }
        };

        model.LearnFromSnapshot(snapshot);

        var weights = model.GetWeights();
        Assert.Equal(-1f, weights[(int)ActionType.Goto]);
    }

    [Fact]
    public void MLModel_LearnFromSnapshot_ExpandsWeights_WhenActionIndexExceedsCapacity()
    {
        var model = new MLModel([]);

        var snapshot = new WoWActivitySnapshot
        {
            CurrentAction = new ActionMessage
            {
                ActionType = ActionType.CastSpell, // index 5
                ActionResult = ResponseResult.Success
            }
        };

        model.LearnFromSnapshot(snapshot);

        var weights = model.GetWeights();
        Assert.True(weights.Count > (int)ActionType.CastSpell);
        Assert.Equal(1f, weights[(int)ActionType.CastSpell]);
    }

    [Fact]
    public void MLModel_GetWeights_NeverNull()
    {
        var model = new MLModel(null!);
        Assert.NotNull(model.GetWeights());
    }

    // ===== DecisionEngine.GetNextActions routing =====

    [Fact]
    public void GetNextActions_ReturnsListOfActions_ForValidSnapshot()
    {
        var snapshot = new WoWActivitySnapshot
        {
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 20, MaxHealth = 100 }
            }
        };

        var actions = DecisionEngine.GetNextActions(snapshot);

        Assert.NotNull(actions);
        Assert.NotEmpty(actions);
    }

    [Fact]
    public void GetNextActions_ReturnsEmptyList_ForEmptySnapshot()
    {
        var actions = DecisionEngine.GetNextActions(new WoWActivitySnapshot());

        Assert.NotNull(actions);
        Assert.Empty(actions);
    }

    [Fact]
    public void GetNextActions_NeverThrows()
    {
        var ex = Record.Exception(() => DecisionEngine.GetNextActions(new WoWActivitySnapshot()));
        Assert.Null(ex);
    }

    // ===== DecisionEngine lifecycle (constructor validation + dispose) =====

    [Fact]
    public void DecisionEngine_Constructor_ThrowsOnNullOrEmptyBinDir()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"des-test-{Guid.NewGuid():N}.db");
        try
        {
            InitializeSqliteDb(dbPath);
            var db = new SQLiteDatabase($"Data Source={dbPath}");

            Assert.Throws<ArgumentException>(() => new DecisionEngine("", db));
            Assert.Throws<ArgumentException>(() => new DecisionEngine("  ", db));
        }
        finally
        {
            TryDeleteFile(dbPath);
        }
    }

    [Fact]
    public void DecisionEngine_Constructor_ThrowsOnNullDb()
    {
        Assert.Throws<ArgumentNullException>(() => new DecisionEngine(Path.GetTempPath(), null!));
    }

    [Fact]
    public void DecisionEngine_Dispose_IsIdempotent()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"des-test-{Guid.NewGuid():N}.db");
        var binDir = Path.Combine(Path.GetTempPath(), $"des-bin-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(binDir);
            InitializeSqliteDb(dbPath);
            var db = new SQLiteDatabase($"Data Source={dbPath}");
            var engine = new DecisionEngine(binDir, db);

            // First dispose
            var ex1 = Record.Exception(() => engine.Dispose());
            Assert.Null(ex1);

            // Second dispose — must not throw
            var ex2 = Record.Exception(() => engine.Dispose());
            Assert.Null(ex2);
        }
        finally
        {
            TryDeleteFile(dbPath);
            TryDeleteDirectory(binDir);
        }
    }

    [Fact]
    public void DecisionEngine_ConstructsSuccessfully_WithValidInputs()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"des-test-{Guid.NewGuid():N}.db");
        var binDir = Path.Combine(Path.GetTempPath(), $"des-bin-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(binDir);
            InitializeSqliteDb(dbPath);
            var db = new SQLiteDatabase($"Data Source={dbPath}");

            using var engine = new DecisionEngine(binDir, db);
            // If we get here without exception, construction succeeded
            Assert.NotNull(engine);
        }
        finally
        {
            TryDeleteFile(dbPath);
            TryDeleteDirectory(binDir);
        }
    }

    // ===== Helpers =====

    private static void InitializeSqliteDb(string dbPath)
    {
        using var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = new System.Data.SQLite.SQLiteCommand(
            "CREATE TABLE IF NOT EXISTS ModelWeights (id INTEGER PRIMARY KEY AUTOINCREMENT, weights BLOB)", conn);
        cmd.ExecuteNonQuery();
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
