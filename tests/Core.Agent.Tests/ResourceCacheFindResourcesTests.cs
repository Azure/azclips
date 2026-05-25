using System.Text.Json;
using Core.Agent.Tools.Cache;
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
/// ~/.cirrus/arm-cache content.
/// </summary>
public sealed class ResourceCacheFindResourcesTests : IDisposable
{
    private readonly string _cacheDir;
    private const string SubA = "00000000-0000-0000-0000-0000000000aa";
    private const string SubB = "00000000-0000-0000-0000-0000000000bb";

    public ResourceCacheFindResourcesTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "cirrus-cache-tests-" + Guid.NewGuid().ToString("N"));
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
        var cache = new ResourceCache(_cacheDir);

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
        var cache = new ResourceCache(_cacheDir);

        var hits = cache.FindResources("zhonxing-test", kind: "resourceGroup", scope: null, top: 5);

        var top = Assert.Single(hits);
        Assert.Equal("zhoxing-test", top.Resource.Name);
        Assert.Equal("fuzzy", top.Match);
        Assert.True(top.Score >= 0.85, $"expected high fuzzy score for 1-char-edit typo, got {top.Score:F3}");
    }

    [Fact]
    public void FindResources_WithRgKind_ShouldNotReturnArmResources()
    {
        // openclaw-zhoxing-260307 is a botservice; it must NOT show up under
        // kind=resourceGroup even though its name contains "zhoxing".
        var cache = new ResourceCache(_cacheDir);

        var hits = cache.FindResources("zhoxing", kind: "resourceGroup", scope: null, top: 10);

        Assert.All(hits, h => Assert.Equal("Microsoft.Resources/resourceGroups", h.Resource.Type));
    }

    // ── No kind filter: must merge all kinds, ranked by score ──

    [Fact]
    public void FindResources_WithoutKindFilter_ShouldFindBothRgAndResource()
    {
        var cache = new ResourceCache(_cacheDir);

        var hits = cache.FindResources("zhoxing", kind: null, scope: null, top: 10);

        Assert.Contains(hits, h => h.Resource.Name == "zhoxing-test"
                                && h.Resource.Type == "Microsoft.Resources/resourceGroups");
        Assert.Contains(hits, h => h.Resource.Name == "openclaw-zhoxing-260307"
                                && h.Resource.Type == "microsoft.botservice/botservices");
    }

    [Fact]
    public void FindResources_WithoutKindFilter_ShouldRankByScoreDescending()
    {
        var cache = new ResourceCache(_cacheDir);

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
        var cache = new ResourceCache(_cacheDir);

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
        var cache = new ResourceCache(_cacheDir);

        var hits = cache.FindResources("Sub A", kind: "subscription", scope: null, top: 5);

        var top = Assert.Single(hits);
        Assert.Equal("Sub A", top.Resource.Name);
        Assert.Equal("Microsoft.Subscription", top.Resource.Type);
    }

    [Fact]
    public void FindResources_ShouldFindTenantByDisplayName()
    {
        var cache = new ResourceCache(_cacheDir);

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
        var cache = new ResourceCache(_cacheDir);

        var hits = cache.FindResources("zhoxing-test", kind: "rolesignment", scope: null, top: 5);

        Assert.Empty(hits);
    }

    // ── Scope filter ──

    [Fact]
    public void FindResources_ShouldFilterRgsBySubscription()
    {
        var cache = new ResourceCache(_cacheDir);

        var hitsInA = cache.FindResources(namePattern: null, kind: "resourceGroup",
            scope: new ScopeFilter(SubA, null), top: 50);
        var hitsInB = cache.FindResources(namePattern: null, kind: "resourceGroup",
            scope: new ScopeFilter(SubB, null), top: 50);

        Assert.All(hitsInA, h => Assert.Equal(SubA, h.Resource.SubscriptionId));
        Assert.All(hitsInB, h => Assert.Equal(SubB, h.Resource.SubscriptionId));
        Assert.NotEmpty(hitsInA);
        Assert.NotEmpty(hitsInB);
    }

    // ── Cache miss is not a panic signal ──

    [Fact]
    public void FindResources_ShouldReturnEmpty_WhenPatternIsNothingLikeAnyRg()
    {
        var cache = new ResourceCache(_cacheDir);

        var hits = cache.FindResources("xyzqwerty", kind: "resourceGroup", scope: null, top: 5);

        Assert.Empty(hits);
    }

    // ── Top-N capping ──

    [Fact]
    public void FindResources_ShouldRespectTopParameter()
    {
        var cache = new ResourceCache(_cacheDir);

        var hits = cache.FindResources(namePattern: null, kind: "resourceGroup",
            scope: null, top: 2);

        Assert.True(hits.Count <= 2);
    }

    // ── Fixture writers ──

    private void SeedDefaultFixture()
    {
        var tenant = "11111111-1111-1111-1111-111111111111";

        WriteEnvelope("tenants.json", new object[]
        {
            new { tenantId = tenant, displayName = "Contoso", defaultDomain = "contoso.onmicrosoft.com" },
        });

        WriteEnvelope("subscriptions.json", new object[]
        {
            new { subscriptionId = SubA, displayName = "Sub A", tenantId = tenant, state = "Enabled", isDefault = true },
            new { subscriptionId = SubB, displayName = "Sub B", tenantId = tenant, state = "Enabled", isDefault = false },
        });

        WriteEnvelope("resource-groups.json", new object[]
        {
            new { id = $"/subscriptions/{SubA}/resourceGroups/zhoxing-test",
                  name = "zhoxing-test", location = "westus", subscriptionId = SubA, tenantId = tenant, tags = (object?)null },
            new { id = $"/subscriptions/{SubA}/resourceGroups/zhwen-domain",
                  name = "zhwen-domain", location = "westus", subscriptionId = SubA, tenantId = tenant, tags = (object?)null },
            new { id = $"/subscriptions/{SubB}/resourceGroups/prod-rg",
                  name = "prod-rg", location = "eastus", subscriptionId = SubB, tenantId = tenant, tags = (object?)null },
        });

        WriteEnvelope("resources.json", new object[]
        {
            new { id = $"/subscriptions/{SubA}/resourceGroups/zhoxing-test/providers/microsoft.botservice/botservices/openclaw-zhoxing-260307",
                  name = "openclaw-zhoxing-260307",
                  type = "microsoft.botservice/botservices",
                  location = "westus",
                  subscriptionId = SubA,
                  tenantId = tenant,
                  resourceGroup = "zhoxing-test",
                  kind = (string?)null, sku = (object?)null, tags = (object?)null,
                  identity = (object?)null, managedBy = (string?)null },
            new { id = $"/subscriptions/{SubB}/resourceGroups/prod-rg/providers/microsoft.compute/virtualmachines/prod-vm-01",
                  name = "prod-vm-01",
                  type = "microsoft.compute/virtualmachines",
                  location = "eastus",
                  subscriptionId = SubB,
                  tenantId = tenant,
                  resourceGroup = "prod-rg",
                  kind = (string?)null, sku = (object?)null, tags = (object?)null,
                  identity = (object?)null, managedBy = (string?)null },
        });
    }

    private void WriteEnvelope(string fileName, object[] items)
    {
        var envelope = new
        {
            schemaVersion = 1,
            cachedAt = DateTime.UtcNow,
            ttlSeconds = 3600,
            itemCount = items.Length,
            items,
        };
        File.WriteAllText(Path.Combine(_cacheDir, fileName),
            JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = false }));
    }
}
