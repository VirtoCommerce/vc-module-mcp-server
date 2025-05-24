angular.module('McpServer')
    .factory('McpServer.webApi', ['$resource', function ($resource) {
        return $resource('api/mcp-server');
    }]);
