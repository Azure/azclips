package core

import tea "github.com/charmbracelet/bubbletea"

type View interface {
	Init() tea.Cmd
	Update(msg tea.Msg) (tea.Model, tea.Cmd)
	View() string
}

type HelpView struct {
	core     *coreImpl
	helpInfo HelpInfo
}

func (v *HelpView) Init() tea.Cmd {
	return nil
}

func (v *HelpView) Update(msg tea.Msg) (tea.Model, tea.Cmd) {
	return v, nil
}

func (v *HelpView) View() string {

	return "View Help"
}

type RunView struct {
	executor CmdExecutor
}

func (v *RunView) Init() tea.Cmd {
	return nil
}

func (v *RunView) Update(msg tea.Msg) (tea.Model, tea.Cmd) {
	return v, nil
}

func (v *RunView) View() string {
	return "View Run"
}
