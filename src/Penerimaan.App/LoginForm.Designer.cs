#nullable disable

namespace Penerimaan.App;

partial class LoginForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblJudul = new Label();
        lblUsername = new Label();
        txtUsername = new TextBox();
        lblPassword = new Label();
        txtPassword = new TextBox();
        btnLogin = new Button();
        lblPesan = new Label();
        lblLokasi = new Label();
        SuspendLayout();

        // lblJudul
        lblJudul.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblJudul.Location = new Point(24, 20);
        lblJudul.Name = "lblJudul";
        lblJudul.Size = new Size(360, 30);
        lblJudul.TabIndex = 0;
        lblJudul.Text = "Penerimaan Paket SSKCKR";
        lblJudul.TextAlign = ContentAlignment.MiddleCenter;

        lblLokasi.Location = new Point(24, 46);
        lblLokasi.Name = "lblLokasi";
        lblLokasi.Size = new Size(360, 22);
        lblLokasi.TabIndex = 7;
        lblLokasi.TextAlign = ContentAlignment.MiddleCenter;

        // lblUsername
        lblUsername.AutoSize = true;
        lblUsername.Location = new Point(24, 92);
        lblUsername.Name = "lblUsername";
        lblUsername.Size = new Size(72, 20);
        lblUsername.TabIndex = 1;
        lblUsername.Text = "Username";

        txtUsername.Location = new Point(120, 89);
        txtUsername.Name = "txtUsername";
        txtUsername.Size = new Size(264, 27);
        txtUsername.TabIndex = 2;

        // lblPassword
        lblPassword.AutoSize = true;
        lblPassword.Location = new Point(24, 130);
        lblPassword.Name = "lblPassword";
        lblPassword.Size = new Size(70, 20);
        lblPassword.TabIndex = 3;
        lblPassword.Text = "Password";

        txtPassword.Location = new Point(120, 127);
        txtPassword.Name = "txtPassword";
        txtPassword.Size = new Size(264, 27);
        txtPassword.TabIndex = 4;
        txtPassword.UseSystemPasswordChar = true;
        txtPassword.KeyDown += txtPassword_KeyDown;

        // btnLogin
        btnLogin.Location = new Point(120, 170);
        btnLogin.Name = "btnLogin";
        btnLogin.Size = new Size(264, 36);
        btnLogin.TabIndex = 5;
        btnLogin.Text = "MASUK";
        btnLogin.UseVisualStyleBackColor = true;
        btnLogin.Click += btnLogin_Click;

        // lblPesan
        lblPesan.ForeColor = Color.Firebrick;
        lblPesan.Location = new Point(24, 216);
        lblPesan.Name = "lblPesan";
        lblPesan.Size = new Size(360, 44);
        lblPesan.TabIndex = 6;
        lblPesan.TextAlign = ContentAlignment.MiddleCenter;

        // LoginForm
        AcceptButton = btnLogin;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(408, 274);
        Controls.Add(lblPesan);
        Controls.Add(btnLogin);
        Controls.Add(txtPassword);
        Controls.Add(lblPassword);
        Controls.Add(txtUsername);
        Controls.Add(lblUsername);
        Controls.Add(lblJudul);
        Controls.Add(lblLokasi);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "LoginForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Masuk - SAKEDAP";
        ResumeLayout(false);
        PerformLayout();
    }

    private Label lblJudul;
    private Label lblLokasi;
    private Label lblUsername;
    private TextBox txtUsername;
    private Label lblPassword;
    private TextBox txtPassword;
    private Button btnLogin;
    private Label lblPesan;
}
