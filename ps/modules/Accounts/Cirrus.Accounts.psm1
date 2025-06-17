[Accounts.CoreWrapper]::Initialize()

class ClassC {
    [string]$Name
}

class ClassB {
    [string]$Name
    [ClassC]$ClassC
}

class ClassA {
    [string]$Name
    [ClassB]$ClassB
    [System.Collections.Generic.List[ClassB]]$ClassBs
}


$obj = [ClassA]::new()
$obj.Name = "ClassAName"
$obj.ClassB = [ClassB]::new()
$obj.ClassB.Name = "ClassBName"
$obj.ClassB.ClassC = [ClassC]::new()
$obj.ClassB.ClassC.Name = "ClassCName"
$obj.ClassBs = @([ClassB]::new(), [ClassB]::new())
$obj.ClassBs[0].Name = "ClassBName0"
$obj.ClassBs[1].Name = "ClassBName1"
$obj.ClassBs[0].ClassC = [ClassC]::new()
$obj.ClassBs[0].ClassC.Name = "ClassCName0"
$obj.ClassBs[1].ClassC = [ClassC]::new()
$obj.ClassBs[1].ClassC.Name = "ClassCName1"

$result = [Accounts.ObjectFlattener]::ToFlatDictionary($obj)
Write-Host $result