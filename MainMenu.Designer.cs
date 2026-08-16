namespace ReactionTimeMeasurementApp;

partial class MainMenu
{
    private System.ComponentModel.IContainer components = null;
    private BufferedPanel contentHostPanel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        contentHostPanel = new BufferedPanel();
        SuspendLayout();
        // 
        // contentHostPanel
        // 
        contentHostPanel.Dock = DockStyle.Fill;
        contentHostPanel.Location = new Point(0, 0);
        contentHostPanel.Name = "contentHostPanel";
        contentHostPanel.Size = new Size(1400, 900);
        contentHostPanel.TabIndex = 0;
        // 
        // MainMenu
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1400, 900);
        Controls.Add(contentHostPanel);
        Name = "MainMenu";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Reakcióidő-mérés Tobii eszközzel";
        ResumeLayout(false);
    }

    #endregion
}

