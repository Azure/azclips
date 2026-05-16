window.BENCHMARK_DATA = {
  "lastUpdate": 1778928792501,
  "repoUrl": "https://github.com/Azure/cirrus",
  "entries": {
    "Cirrus PS Benchmarks": [
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e2b98529ac515a4bc8ed9f83a3a6281a4cf25880",
          "message": "Implement benchmark workflows and update module configuration scripts (#227)\n\n1. Fix benchmarking failures from previous merges\n2. Add pipeline action to run benchmarks on every PR commit\n3. Add pipeline action to post comment on PRs initiated from fork repo.\n\nIssue for reference: https://github.com/Azure/CLIPS/issues/165\n\nThe following needs to be completed for the both workflows to be fully\nfunctional.\n1. Merge atleast `benchmark-comment.yml` into `main` branch.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-05-15T13:20:43+10:00",
          "tree_id": "f55762d1f006a44a0ca03c8b6182461149985ebf",
          "url": "https://github.com/Azure/cirrus/commit/e2b98529ac515a4bc8ed9f83a3a6281a4cf25880"
        },
        "date": 1778815408854,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 23280221.8,
            "unit": "ns",
            "range": "± 2148242.5364787607"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 23031720.2,
            "unit": "ns",
            "range": "± 2348817.1265382073"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 22952618.8,
            "unit": "ns",
            "range": "± 2150444.169441792"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yabhu@microsoft.com",
            "name": "Yabo Hu",
            "username": "VeryEarly"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "185c465e63ab0a110ae8b8307d5dc8fe692d0a88",
          "message": "Fix build script to update path in artifacts instead of src  (#240)\n\nfix GHA bug:\n\nhttps://github.com/Azure/cirrus/actions/runs/25958312170/job/76308977496?pr=239\n- build time patch psd1 should target artifacts instead of src\n- test case should also target artifacts/debug instead of src",
          "timestamp": "2026-05-16T18:36:40+08:00",
          "tree_id": "73aaac3dc4c690ed52d3b4a65ea24c3189c03a02",
          "url": "https://github.com/Azure/cirrus/commit/185c465e63ab0a110ae8b8307d5dc8fe692d0a88"
        },
        "date": 1778927952466,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 23044144.8,
            "unit": "ns",
            "range": "± 2463120.948304955"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22315977.4,
            "unit": "ns",
            "range": "± 2849797.3938796595"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21528406.2,
            "unit": "ns",
            "range": "± 2122087.142965906"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "110961157+NoriZC@users.noreply.github.com",
            "name": "NoriZC",
            "username": "NoriZC"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "17e5705c9453b08708a17ed8418ceff151e029b6",
          "message": "remove threshold failure limitation (#243)",
          "timestamp": "2026-05-16T10:50:59Z",
          "tree_id": "fb8d1dc1f6880881b200564a1b7bf7c03450a5de",
          "url": "https://github.com/Azure/cirrus/commit/17e5705c9453b08708a17ed8418ceff151e029b6"
        },
        "date": 1778928792480,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 21859827.4,
            "unit": "ns",
            "range": "± 3259288.3877778905"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22284199.4,
            "unit": "ns",
            "range": "± 1990767.5649085203"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21564648.4,
            "unit": "ns",
            "range": "± 1990033.3949204972"
          }
        ]
      }
    ]
  }
}