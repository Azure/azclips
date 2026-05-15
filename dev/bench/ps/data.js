window.BENCHMARK_DATA = {
  "lastUpdate": 1778815408886,
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
      }
    ]
  }
}