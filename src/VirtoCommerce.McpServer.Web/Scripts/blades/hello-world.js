angular.module('McpServer')
    .controller('McpServer.helloWorldController', ['$scope', 'McpServer.webApi', function ($scope, api) {
        var blade = $scope.blade;
        blade.title = 'McpServer';

        blade.refresh = function () {
            api.get(function (data) {
                blade.title = 'McpServer.blades.hello-world.title';
                blade.data = data.result;
                blade.isLoading = false;
            });
        };

        blade.refresh();
    }]);
