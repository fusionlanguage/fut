#!perl
use strict;
my %t = map { m!/(\w+)/\w+\.txt$! or die; $1 => 1 } @ARGV;
my %p;
while (<>) {
	if (/^PASSED/) {
		$ARGV =~ m!/(\w+)\.txt$!g or die;
		$p{$1}++;
	}
	else {
		print "$ARGV $_";
	}
}
print "PASSED ";
for (sort keys %p) {
	print "$_=$p{$_} ";
}
print "of ", scalar(keys %t), " tests\n";
