// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

[ConfigurationSection("survey")]
public sealed class SurveyOptions
{
    [ConfigurationKey("enabled", Description = "Enable or disable the message inviting users to submit feedback via survey.")]
    public bool Enabled { get; set; } = true;
}
