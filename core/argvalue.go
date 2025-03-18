package core

type ArgValue interface {
	isArgValue()
}

type ArgUndefinedType struct{}

var ArgUndefined = ArgUndefinedType{}

func (ArgUndefinedType) isArgValue() {}

type ArgStrValue struct {
	data    string
	isPatch bool
}

type ArgIntValue struct {
	data    int
	isPatch bool
}

func (v *ArgStrValue) isArgValue() {}
