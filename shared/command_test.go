package shared

import (
	"fmt"
	"testing"
)

func TestCMDUri(t *testing.T) {
	// command level
	uri := CMDUri{ModuleName: "rest", Cmds: []string{"network", "vnet", "create"}, Executable: true}
	if fmt.Sprintf("%s", uri) != "az://rest/network/vnet/create" {
		t.Errorf("CMDUri is not correct '%s'", uri)
	}

	uri = CMDUri{ModuleName: "rest", Cmds: []string{"network", "vnet", "create"}, Executable: true, Version: "2024-01-01", Params: map[string]string{"param1": "value1", "param2": "value2"}}
	if fmt.Sprintf("%s", uri) != "az://rest/network/vnet/create?version=2024-01-01&param1=value1&param2=value2" {
		t.Errorf("CMDUri is not correct '%s'", uri)
	}

	// group level
	uri = CMDUri{ModuleName: "rest", Cmds: []string{"network", "vnet"}, Executable: false}
	if fmt.Sprintf("%s", uri) != "az://rest/network/vnet/" {
		t.Errorf("CMDUri is not correct '%s'", uri)
	}

	uri = CMDUri{ModuleName: "rest", Cmds: []string{"network", "vnet"}, Executable: false, Version: "2024-01-01", Params: map[string]string{"param1": "value1", "param2": "value2"}}
	if fmt.Sprintf("%s", uri) != "az://rest/network/vnet/?version=2024-01-01&param1=value1&param2=value2" {
		t.Errorf("CMDUri is not correct '%s'", uri)
	}

	// module level
	uri = CMDUri{ModuleName: "rest", Cmds: []string{}, Executable: false}
	if fmt.Sprintf("%s", uri) != "az://rest/" {
		t.Errorf("CMDUri is not correct '%s'", uri)
	}

	uri = CMDUri{ModuleName: "rest", Cmds: []string{}, Executable: false, Version: "2024-01-01", Params: map[string]string{"param1": "value1", "param2": "value2"}}
	if fmt.Sprintf("%s", uri) != "az://rest/?version=2024-01-01&param1=value1&param2=value2" {
		t.Errorf("CMDUri is not correct '%s'", uri)
	}

	// parse valid uri
	u, err := ParseCMDUri("az://rest/network/vnet/create")
	if err != nil {
		t.Errorf("ParseCMDUri should return nil error for valid uri")
	}
	if u.ModuleName != "rest" || len(u.Cmds) != 3 || u.Cmds[0] != "network" || u.Cmds[1] != "vnet" || u.Cmds[2] != "create" || u.Executable != true || u.Version != "" || len(u.Params) != 0 {
		t.Errorf("ParseCMDUri should return correct uri %s", u)
	}

	// parse valid uri
	u, err = ParseCMDUri("az://rest/network/vnet/create?version=2024-01-01&param1=value1&param2=value2")
	if err != nil {
		t.Errorf("ParseCMDUri should return nil error for valid uri")
	}
	if u.ModuleName != "rest" || len(u.Cmds) != 3 || u.Cmds[0] != "network" || u.Cmds[1] != "vnet" || u.Cmds[2] != "create" || u.Executable != true || u.Version != "2024-01-01" || len(u.Params) != 2 || u.Params["param1"] != "value1" || u.Params["param2"] != "value2" {
		t.Errorf("ParseCMDUri should return correct uri %s", u)
	}

	u, err = ParseCMDUri("az://rest/network/vnet/")
	if err != nil {
		t.Errorf("ParseCMDUri should return nil error for valid uri")
	} else if u.ModuleName != "rest" || len(u.Cmds) != 2 || u.Cmds[0] != "network" || u.Cmds[1] != "vnet" || u.Executable != false || u.Version != "" || len(u.Params) != 0 {
		t.Errorf("ParseCMDUri should return correct uri %s", u)
	}

	u, err = ParseCMDUri("az://rest/network/vnet/?version=2024-01-01&param1=value1&param2=value2")
	if err != nil {
		t.Errorf("ParseCMDUri should return nil error for valid uri")
	} else if u.ModuleName != "rest" || len(u.Cmds) != 2 || u.Cmds[0] != "network" || u.Cmds[1] != "vnet" || u.Executable != false || u.Version != "2024-01-01" || len(u.Params) != 2 || u.Params["param1"] != "value1" || u.Params["param2"] != "value2" {
		t.Errorf("ParseCMDUri should return correct uri %s", u)
	}

	u, err = ParseCMDUri("az://rest/")
	if err != nil {
		t.Errorf("ParseCMDUri should return nil error for valid uri")
	} else if u.ModuleName != "rest" || len(u.Cmds) != 0 || u.Executable != false || u.Version != "" || len(u.Params) != 0 {
		t.Errorf("ParseCMDUri should return correct uri %s", u)
	}

	u, err = ParseCMDUri("az://rest/?version=2024-01-01&param1=value1&param2=value2")
	if err != nil {
		t.Errorf("ParseCMDUri should return nil error for valid uri")
	} else if u.ModuleName != "rest" || len(u.Cmds) != 0 || u.Executable != false || u.Version != "2024-01-01" || len(u.Params) != 2 || u.Params["param1"] != "value1" || u.Params["param2"] != "value2" {
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
