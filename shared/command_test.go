package shared

import (
	"fmt"
	"testing"
)

func TestCMDUri(t *testing.T) {
	// command level
	uri := CMDUri{ModuleName: "rest", Cmds: []string{"network", "vnet", "create"}, Executable: true}
	if fmt.Sprintf("%s", uri) != "az://rest/network/vnet/create" {
		t.Errorf("CMDUri is not correct %s", uri)
	}

	// group level
	uri = CMDUri{ModuleName: "rest", Cmds: []string{"network", "vnet"}, Executable: false}
	if fmt.Sprintf("%s", uri) != "az://rest/network/vnet/" {
		t.Errorf("CMDUri is not correct %s", uri)
	}

	// module level
	uri = CMDUri{ModuleName: "rest", Cmds: []string{}, Executable: false}
	if fmt.Sprintf("%s", uri) != "az://rest/" {
		t.Errorf("CMDUri is not correct %s", uri)
	}

	// parse valid uri
	u, err := ParseCMDUri("az://rest/network/vnet/create")
	if err != nil {
		t.Errorf("ParseCMDUri should return nil error for valid uri")
	}
	if u.ModuleName != "rest" || len(u.Cmds) != 3 || u.Cmds[0] != "network" || u.Cmds[1] != "vnet" || u.Cmds[2] != "create" || u.Executable != true {
		t.Errorf("ParseCMDUri should return correct uri %s", u)
	}

	u, err = ParseCMDUri("az://rest/network/vnet/")
	if err != nil {
		t.Errorf("ParseCMDUri should return nil error for valid uri")
	} else if u.ModuleName != "rest" || len(u.Cmds) != 2 || u.Cmds[0] != "network" || u.Cmds[1] != "vnet" || u.Executable != false {
		t.Errorf("ParseCMDUri should return correct uri %s", u)
	}

	u, err = ParseCMDUri("az://rest/")
	if err != nil {
		t.Errorf("ParseCMDUri should return nil error for valid uri")
	} else if u.ModuleName != "rest" || len(u.Cmds) != 0 || u.Executable != false {
		t.Errorf("ParseCMDUri should return correct uri %s", u)
	}

	// invalid uri
	_, err = ParseCMDUri("")
	if err == nil {
		t.Errorf("ParseCMDUri should return error for empty string")
	}

	// invalid uri
	_, err = ParseCMDUri("az://")
	if err == nil {
		t.Errorf("ParseCMDUri should return error for invalid uri")
	}

	// invalid uri
	_, err = ParseCMDUri("az:/")
	if err == nil {
		t.Errorf("ParseCMDUri should return error for invalid uri")
	}

	// invalid uri
	_, err = ParseCMDUri("az://rest")
	if err == nil {
		t.Errorf("ParseCMDUri should return error for invalid uri")
	}

	// invalid uri
	_, err = ParseCMDUri("abz://rest/")
	if err == nil {
		t.Errorf("ParseCMDUri should return error for invalid uri")
	}

	// invalid uri
	_, err = ParseCMDUri("az:///network/")
	if err == nil {
		t.Errorf("ParseCMDUri should return error for invalid uri")
	}

	// invalid uri
	_, err = ParseCMDUri("az://rest//network/")
	if err == nil {
		t.Errorf("ParseCMDUri should return error for invalid uri")
	}
}
