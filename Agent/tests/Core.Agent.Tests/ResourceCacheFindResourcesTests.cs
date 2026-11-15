using System.Text.Json;
using Core.Agent.Tools.Resource;
using Microsoft.Extensions.AI;
using Tests.Common.Stubs;
using Core.Agent.Tools.Resource.Cache;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Tests for ResourceCache.FindResources — the search backbone for the
/// three diagnostic tools (find_resources, get_resource_tree,
/// get_resource_details).
///
/// Includes a regression fixture for the type=resourceGroups bug: the
/// initial implementation only indexed the Resources collection, so any
/// search whose target was a resource group returned 0 hits regardless
/// of pattern, and the diagnose model would panic-search with progressively
/// broader patterns.
///
/// Each test owns its own temp cache directory and ResourceCache instance
/// so they are fully parallel-safe and independent of the user's real
/// ~/.azclips/agent/arm content.
/// </summary>
public sealed class ResourceCacheFindResourcesTests : IDisposable
{
    private readonly string _cacheDir;
    // subscriptions / resource-groups / resources live under <root>/<TenantId>/
    // in the real cache; the fixture mirrors that so the per-tenant load path
    // (not the legacy root-level layout) is what these tests actually exercise.
    private const string TenantId = "11111111-1111-1111-1111-111111111111";
    private const string SubA = "00000000-0000-0000-0000-0000000000aa";
    private const string SubB = "00000000-0000-0000-0000-0000000000bb";

    public ResourceCacheFindResourcesTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "azclips-cache-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheDir);
        SeedDefaultFixture();
    }

    public void Dispose()
    {
        try { Directory.Delete(_cacheDir, recursive: true); } catch { /* best-effort */ }
    }

    // ── REGRESSION: type=resourceGroups must search ResourceGroups, not Resources ──

    [Fact]
    public void FindResources_ShouldFindRgByExactName_WhenKindIsResourceGroup()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("zhoxing-test", kind: "resourceGroup", scope: null, top: 5);

        var top = Assert.Single(hits);
        Assert.Equal("zhoxing-test", top.Resource.Name);
        Assert.Equal("Microsoft.Resources/resourceGroups", top.Resource.Type);
        Assert.Equal("exact", top.Match);
        Assert.Equal(1.0, top.Score);
    }

    [Fact]
    public void FindResources_ShouldFindRgViaLevenshtein_WhenPatternIsTypoOfRgName()
    {
        // Reproduces the original failure: model called
        //   find_resources(pattern="zhonxing-test", kind="resourceGroup")
        // Pre-fix, this returned 0 hits because RGs weren't searchable.
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("zhonxing-test", kind: "resourceGroup", scope: null, top: 5);

        var top = Assert.Single(hits);
        Assert.Equal("zhoxing-test", top.Resource.Name);
        Assert.Equal("similar", top.Match);
        Assert.Equal(0.923, top.Score, 3);
    }

    [Fact]
    public void FindResources_WithRgKind_ShouldNotReturnArmResources()
    {
        // openclaw-zhoxing-260307 is a botservice; it must NOT show up under
        // kind=resourceGroup even though its name contains "zhoxing".
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("zhoxing", kind: "resourceGroup", scope: null, top: 10);

        Assert.All(hits, h => Assert.Equal("Microsoft.Resources/resourceGroups", h.Resource.Type));
    }

    // ── No kind filter: must merge all kinds, ranked by score ──

    [Fact]
    public void FindResources_WithoutKindFilter_ShouldFindBothRgAndResource()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources(namePattern: null, kind: null, scope: null, top: 50);

        Assert.Contains(hits, h => h.Resource.Name == "zhoxing-test"
                                && h.Resource.Type == "Microsoft.Resources/resourceGroups");
        Assert.Contains(hits, h => h.Resource.Name == "openclaw-zhoxing-260307"
                                && h.Resource.Type == "microsoft.botservice/botservices");
    }

    [Fact]
    public void FindResources_WithoutKindFilter_ShouldRankByScoreDescending()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("zhoxing", kind: null, scope: null, top: 10);

        for (var i = 1; i < hits.Count; i++)
        {
            Assert.True(hits[i - 1].Score >= hits[i].Score,
                $"hit {i - 1} ({hits[i - 1].Resource.Name} score={hits[i - 1].Score}) " +
                $"must rank above hit {i} ({hits[i].Resource.Name} score={hits[i].Score})");
        }
    }

    // ── Resource search via kind=resource ──

    [Fact]
    public void FindResources_ShouldFindResourceByExactName_WhenKindIsResource()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("openclaw-zhoxing-260307",
            kind: "resource", scope: null, top: 5);

        var top = Assert.Single(hits);
        Assert.Equal("exact", top.Match);
        Assert.Equal("openclaw-zhoxing-260307", top.Resource.Name);
        Assert.Equal("microsoft.botservice/botservices", top.Resource.Type);
    }

    // ── Subscription / Tenant kinds ──

    [Fact]
    public void FindResources_ShouldFindSubscriptionByDisplayName()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("Sub A", kind: "subscription", scope: null, top: 5);

        // "Sub B" is 0.8 similar, so it follows the exact match.
        Assert.Equal(new[] { "Sub A", "Sub B" }, hits.Select(h => h.Resource.Name));
        var top = hits[0];
        Assert.Equal("exact", top.Match);
        Assert.Equal("Microsoft.Subscription", top.Resource.Type);
    }

    [Fact]
    public void FindResources_ShouldFindTenantByDisplayName()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("Contoso", kind: "tenant", scope: null, top: 5);

        var top = Assert.Single(hits);
        Assert.Equal("Contoso", top.Resource.Name);
        Assert.Equal("Microsoft.Tenant", top.Resource.Type);
    }

    // ── Unknown kind ──

    [Fact]
    public void FindResources_ShouldReturnEmpty_WhenKindIsUnknown()
    {
        // ResourceCache returns empty for unknown kind; tool wrappers turn
        // this into a structured error to the model.
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("zhoxing-test", kind: "rolesignment", scope: null, top: 5);

        Assert.Empty(hits);
    }

    // ── Scope filter ──

    [Fact]
    public void FindResources_ShouldFilterRgsBySubscription()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hitsInA = cache.FindResources(namePattern: null, kind: "resourceGroup",
            scope: new ScopeFilter(SubA, null), top: 50);
        var hitsInB = cache.FindResources(namePattern: null, kind: "resourceGroup",
            scope: new ScopeFilter(SubB, null), top: 50);

        Assert.All(hitsInA, h => Assert.Equal(SubA, h.Resource.SubscriptionId));
        Assert.All(hitsInB, h => Assert.Equal(SubB, h.Resource.SubscriptionId));
        Assert.NotEmpty(hitsInA);
        Assert.NotEmpty(hitsInB);
    }

    // ── Similarity ranking: 1 - Levenshtein / max(len), keep >= 0.75 ──

    [Theory]
    [InlineData("azclips-test-rg", "azclips-test-rg", 1.0, "exact")]
    [InlineData("AZCLIPS-Test-RG", "azclips-test-rg", 1.0, "exact")]
    [InlineData("azclips-tset-rg", "azclips-test-rg", 0.867, "similar")]
    [InlineData("prd-rg", "prod-rg", 0.857, "similar")]
    [InlineData("abxd", "abcd", 0.75, "similar")]
    public void FindResources_SimilarityAtOrAboveThreshold_Matches(
        string pattern, string name, double score, string match)
    {
        var cache = CacheWithResourceGroups(name);

        var hits = cache.FindResources(pattern, kind: "resourceGroup", scope: null, top: 5);

        var top = Assert.Single(hits);
        Assert.Equal(name, top.Resource.Name);
        Assert.Equal(match, top.Match);
        Assert.Equal(score, top.Score, 3);
    }

    [Theory]
    [InlineData("abd", "abc")]                                   // 0.667
    [InlineData("azclips-eval-id", "azclips-evaluation")]        // 0.722
    [InlineData("azclips-eval-vnet", "azclis-evaluation-vnet")]  // 0.682
    [InlineData("zh", "zhoxing-test")]
    public void FindResources_SimilarityBelowThreshold_ReturnsNothing(string pattern, string name)
    {
        var cache = CacheWithResourceGroups(name);

        var hits = cache.FindResources(pattern, kind: "resourceGroup", scope: null, top: 5);

        Assert.Empty(hits);
    }

    [Fact]
    public void FindResources_EqualSimilarity_OrdersByName()
    {
        var cache = CacheWithResourceGroups("web2", "web1");

        var hits = cache.FindResources("web", kind: "resourceGroup", scope: null, top: 5);

        Assert.Equal(new[] { "web1", "web2" }, hits.Select(h => h.Resource.Name));
        Assert.All(hits, h => Assert.Equal(0.75, h.Score, 3));
        Assert.All(hits, h => Assert.Equal("similar", h.Match));
    }

    [Fact]
    public void FindResources_WithoutKind_EqualSimilarityAcrossKinds_OrdersByName()
    {
        // web2 is a resource group and web1 a resource; resource groups are searched first.
        var cache = CacheWithResourceGroups("web2");
        WriteEnvelope("resources.json", new object[] { Resource("web2", "web1") }, TenantId);

        var hits = cache.FindResources("web", kind: null, scope: null, top: 5);

        Assert.Equal(new[] { "web1", "web2" }, hits.Select(h => h.Resource.Name));
    }

    [Fact]
    public void FindResources_TopZero_ReturnsNothing()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("prod-rg", kind: "resourceGroup", scope: null, top: 0);

        Assert.Empty(hits);
    }

    [Fact]
    public async Task FindResourcesTool_ReturnsMatchKindAndOrderedCandidates()
    {
        var cache = CacheWithResourceGroups("web2", "web1", "Web");
        var tools = new CachedResourceTools(new ArmDetailsClient(new StubAuthService()),
            new StubProfileService(seedDefault: false), activityCreator: null, cache);

        var output = (await tools.FindResources.InvokeAsync(new AIFunctionArguments
        {
            ["namePattern"] = "web", ["kind"] = "resourceGroup", ["top"] = 10,
        }))!.ToString()!;

        using var doc = JsonDocument.Parse(output);
        var rows = doc.RootElement.GetProperty("matches").EnumerateArray()
            .Select(m => (m.GetProperty("name").GetString(), m.GetProperty("matchKind").GetString(),
                m.GetProperty("score").GetDouble()))
            .ToArray();
        Assert.Equal(new (string?, string?, double)[]
        {
            ("Web", "exact", 1.0), ("web1", "similar", 0.75), ("web2", "similar", 0.75),
        }, rows);
        Assert.Equal(3, doc.RootElement.GetProperty("matchCount").GetInt32());
    }

    // ── Cache miss is not a panic signal ──

    [Fact]
    public void FindResources_ShouldReturnEmpty_WhenPatternIsNothingLikeAnyRg()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources("xyzqwerty", kind: "resourceGroup", scope: null, top: 5);

        Assert.Empty(hits);
    }

    // ── Top-N capping ──

    [Fact]
    public void FindResources_ShouldRespectTopParameter()
    {
        var cache = new ResourceCache(_cacheDir, currentTenantId: () => TenantId);

        var hits = cache.FindResources(namePattern: null, kind: "resourceGroup",
            scope: null, top: 2);

        Assert.True(hits.Count <= 2);
    }

    // ── Fixture writers ──

    private void SeedDefaultFixture()
    {
        // tenants.json is shared across tenants → stays at the cache root.
        WriteEnvelope("tenants.json", new object[]
        {
            new { tenantId = TenantId, displayName = "Contoso", defaultDomain = "contoso.onmicrosoft.com" },
        });

        // subscriptions / resource-groups / resources are per-tenant → tenant subdir.
        WriteEnvelope("subscriptions.json", new object[]
        {
            new { subscriptionId = SubA, displayName = "Sub A", tenantId = TenantId, state = "Enabled" },
            new { subscriptionId = SubB, displayName = "Sub B", tenantId = TenantId, state = "Enabled" },
        }, TenantId);

        WriteEnvelope("resource-groups.json", new object[]
        {
            new { id = $"/subscriptions/{SubA}/resourceGroups/zhoxing-test",
                  name = "zhoxing-test", location = "westus", subscriptionId = SubA, tenantId = TenantId, tags = (object?)null },
            new { id = $"/subscriptions/{SubA}/resourceGroups/zhwen-domain",
                  name = "zhwen-domain", location = "westus", subscriptionId = SubA, tenantId = TenantId, tags = (object?)null },
            new { id = $"/subscriptions/{SubB}/resourceGroups/prod-rg",
                  name = "prod-rg", location = "eastus", subscriptionId = SubB, tenantId = TenantId, tags = (object?)null },
            new { id = $"/subscriptions/{SubB}/resourceGroups/web1",
                  name = "web1", location = "eastus", subscriptionId = SubB, tenantId = TenantId, tags = (object?)null },
        }, TenantId);

        WriteEnvelope("resources.json", new object[]
        {
            new { id = $"/subscriptions/{SubA}/resourceGroups/zhoxing-test/providers/microsoft.botservice/botservices/openclaw-zhoxing-260307",
                  name = "openclaw-zhoxing-260307",
                  type = "microsoft.botservice/botservices",
                  location = "westus",
                  subscriptionId = SubA,
                  tenantId = TenantId,
                  resourceGroup = "zhoxing-test",
                  kind = (string?)null, sku = (object?)null, tags = (object?)null,
                  identity = (object?)null, managedBy = (string?)null },
            new { id = $"/subscriptions/{SubB}/resourceGroups/prod-rg/providers/microsoft.compute/virtualmachines/prod-vm-01",
                  name = "prod-vm-01",
                  type = "microsoft.compute/virtualmachines",
                  location = "eastus",
                  subscriptionId = SubB,
                  tenantId = TenantId,
                  resourceGroup = "prod-rg",
                  kind = (string?)null, sku = (object?)null, tags = (object?)null,
                  identity = (object?)null, managedBy = (string?)null },
        }, TenantId);
    }

    // Replaces the resource groups with the given names (all in SubA) and clears resources.
    private ResourceCache CacheWithResourceGroups(params string[] names)
    {
        WriteEnvelope("resource-groups.json", names.Select(n => (object)new
        {
            id = $"/subscriptions/{SubA}/resourceGroups/{n}",
            name = n, location = "westus", subscriptionId = SubA, tenantId = TenantId, tags = (object?)null,
        }).ToArray(), TenantId);
        WriteEnvelope("resources.json", Array.Empty<object>(), TenantId);
        return new ResourceCache(_cacheDir, currentTenantId: () => TenantId);
    }

    private static object Resource(string resourceGroup, string name) => new
    {
        id = $"/subscriptions/{SubA}/resourceGroups/{resourceGroup}/providers/microsoft.web/sites/{name}",
        name,
        type = "microsoft.web/sites",
        location = "westus",
        subscriptionId = SubA,
        tenantId = TenantId,
        resourceGroup,
        kind = (string?)null, sku = (object?)null, tags = (object?)null,
        identity = (object?)null, managedBy = (string?)null,
    };

    // subDir null → cache root (tenants.json); non-null → <root>/<subDir>/ (per-tenant files).
    private void WriteEnvelope(string fileName, object[] items, string? subDir = null)
    {
        var envelope = new
        {
            schemaVersion = 1,
            cachedAt = DateTime.UtcNow,
            ttlSeconds = 3600,
            itemCount = items.Length,
            items,
        };
        var dir = subDir is null ? _cacheDir : Path.Combine(_cacheDir, subDir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName),
            JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = false }));
    }
}
