package shared

import "testing"

func TestCMDUri(t *testing.T) {
	uri := BuildCMDUri("rest", []string{"network", "vnet", "create"}, false)
	if uri != "az://rest/network/vnet/create" {
		t.Errorf("CMDUri is not correct %s", uri)
	}

	uri = BuildCMDUri("rest", []string{"network", "vnet"}, true)
	if uri != "az://rest/network/vnet/" {
		t.Errorf("CMDUri is not correct %s", uri)
	}

	uri = BuildCMDUri("rest", []string{}, true)
	if uri != "az://rest/" {
		t.Errorf("CMDUri is not correct %s", uri)
	}

	uri = BuildCMDUri("rest", []string{"network", "vnet", "create"}, false)
	if uri != "az://rest/network/vnet/create" {
		t.Errorf("CMDUri is not correct %s", uri)
	}

	uri = BuildCMDUri("rest", []string{"network", "vnet", "create"}, false)
	if !uri.IsCommandLevel() {
		t.Errorf("Expected command level URI but got group level")
	}

	uri = BuildCMDUri("rest", []string{"network", "vnet"}, true)
	if uri.IsCommandLevel() {
		t.Errorf("Expected group level URI but got command level")
	}

	uri = BuildCMDUri("rest", []string{}, true)
	if !uri.IsModuleLevel() {
		t.Errorf("Expected module level URI but got command/group level")
	}
}
