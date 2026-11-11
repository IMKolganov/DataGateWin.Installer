namespace DataGateWin.Installer;

internal enum InstallerWizardStep
{
    Welcome,
    Policy,
    Path,
    Install,
    Finish
}

internal static class InstallerWizardRules
{
    public static InstallerWizardStep GetInitialStep(bool isUpdateMode) =>
        isUpdateMode ? InstallerWizardStep.Install : InstallerWizardStep.Welcome;

    public static InstallerWizardStep? GetPreviousStep(InstallerWizardStep step, bool isUpdateMode) =>
        isUpdateMode ? null : step switch
        {
            InstallerWizardStep.Policy => InstallerWizardStep.Welcome,
            InstallerWizardStep.Path => InstallerWizardStep.Policy,
            _ => null
        };

    public static bool IsNextEnabled(
        InstallerWizardStep step,
        bool policyAccepted,
        string? installPath,
        bool installCompleted,
        bool installFailed = false) =>
        step switch
        {
            InstallerWizardStep.Welcome => true,
            InstallerWizardStep.Policy => policyAccepted,
            InstallerWizardStep.Path => !string.IsNullOrWhiteSpace(installPath),
            InstallerWizardStep.Install => installCompleted || installFailed,
            InstallerWizardStep.Finish => true,
            _ => false
        };
}
