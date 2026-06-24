namespace DataGateWin.Installer;

internal enum InstallerWizardStep
{
    Policy,
    Path,
    Install,
    Finish
}

internal static class InstallerWizardRules
{
    public static InstallerWizardStep GetInitialStep(bool isUpdateMode) =>
        isUpdateMode ? InstallerWizardStep.Install : InstallerWizardStep.Policy;

    public static bool IsNextEnabled(
        InstallerWizardStep step,
        bool policyAccepted,
        string? installPath,
        bool installCompleted) =>
        step switch
        {
            InstallerWizardStep.Policy => policyAccepted,
            InstallerWizardStep.Path => !string.IsNullOrWhiteSpace(installPath),
            InstallerWizardStep.Install => installCompleted,
            InstallerWizardStep.Finish => true,
            _ => false
        };
}
