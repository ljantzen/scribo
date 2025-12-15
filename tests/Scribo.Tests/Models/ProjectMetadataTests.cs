using FluentAssertions;
using Scribo.Models;
using Xunit;

namespace Scribo.Tests.Models;

public class ProjectMetadataTests
{
    [Fact]
    public void ProjectMetadata_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var metadata = new ProjectMetadata();

        // Assert
        metadata.Title.Should().BeEmpty();
        metadata.Author.Should().BeEmpty();
        metadata.WordCountTargets.Should().NotBeNull();
        metadata.Statistics.Should().NotBeNull();
        metadata.Keywords.Should().NotBeNull();
        metadata.Tags.Should().NotBeNull();
        metadata.CustomFields.Should().NotBeNull();
        metadata.CompileSettings.Should().NotBeNull();
        metadata.DocumentOrganization.Should().NotBeNull();
        metadata.ResearchSettings.Should().NotBeNull();
        metadata.Settings.Should().NotBeNull();
    }

    [Fact]
    public void WordCountTargets_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var targets = new WordCountTargets();

        // Assert
        targets.TargetWordCount.Should().Be(0);
        targets.TargetCharacterCount.Should().Be(0);
        targets.ShowTargetInStatusBar.Should().BeTrue();
        targets.IncludeNotesInCount.Should().BeFalse();
    }

    [Fact]
    public void ProjectStatistics_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var stats = new ProjectStatistics();

        // Assert
        stats.TotalWordCount.Should().Be(0);
        stats.TotalCharacterCount.Should().Be(0);
        stats.TotalPageCount.Should().Be(0);
    }

    [Fact]
    public void SessionInfo_ShouldInitializeWithStartTime()
    {
        // Arrange
        var beforeCreation = DateTime.Now;

        // Act
        var session = new SessionInfo();
        var afterCreation = DateTime.Now;

        // Assert
        session.StartTime.Should().BeAfter(beforeCreation.AddSeconds(-1));
        session.StartTime.Should().BeBefore(afterCreation.AddSeconds(1));
        session.EndTime.Should().BeNull();
        session.WordsWritten.Should().Be(0);
    }

    [Fact]
    public void CompileSettings_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var settings = new CompileSettings();

        // Assert
        settings.OutputFormat.Should().Be("PDF");
        settings.PageSize.Should().Be("A4");
        settings.FontSize.Should().Be(12);
        settings.LineSpacing.Should().Be(1.5);
        settings.IncludeTitlePage.Should().BeTrue();
    }

    [Fact]
    public void DocumentOrganizationSettings_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var settings = new DocumentOrganizationSettings();

        // Assert
        settings.ShowBinderInSidebar.Should().BeTrue();
        settings.DefaultDocumentType.Should().Be("Chapter");
        settings.AutoNumberDocuments.Should().BeFalse();
    }

    [Fact]
    public void ResearchFolderSettings_ShouldHaveDefaultAllowedExtensions()
    {
        // Arrange & Act
        var settings = new ResearchFolderSettings();

        // Assert
        settings.AllowedResearchExtensions.Should().NotBeEmpty();
        settings.AllowedResearchExtensions.Should().Contain(".pdf");
        settings.AllowedResearchExtensions.Should().Contain(".txt");
    }

    [Fact]
    public void ProjectSettings_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var settings = new ProjectSettings();

        // Assert
        settings.AutoSave.Should().BeTrue();
        settings.AutoSaveIntervalMinutes.Should().Be(5);
        settings.BackupOnSave.Should().BeTrue();
        settings.DefaultFontSize.Should().Be(14);
        settings.WordWrap.Should().BeTrue();
    }

    [Fact]
    public void ProjectMetadata_ShouldAllowSettingCustomFields()
    {
        // Arrange
        var metadata = new ProjectMetadata();

        // Act
        metadata.CustomFields["Genre"] = "Science Fiction";
        metadata.CustomFields["Status"] = "In Progress";

        // Assert
        metadata.CustomFields.Should().HaveCount(2);
        metadata.CustomFields["Genre"].Should().Be("Science Fiction");
        metadata.CustomFields["Status"].Should().Be("In Progress");
    }

    [Fact]
    public void ProjectMetadata_ShouldAllowAddingKeywords()
    {
        // Arrange
        var metadata = new ProjectMetadata();

        // Act
        metadata.Keywords.Add("fantasy");
        metadata.Keywords.Add("adventure");
        metadata.Keywords.Add("magic");

        // Assert
        metadata.Keywords.Should().HaveCount(3);
        metadata.Keywords.Should().Contain("fantasy");
    }

    [Fact]
    public void ProjectMetadata_ShouldTrackVersion()
    {
        // Arrange & Act
        var metadata = new ProjectMetadata();

        // Assert
        metadata.Version.Should().Be("1.0");
    }
}
